using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Listens on UDP port 5005 for synergy packets from the Python MediaPipe sender.
///
/// Packet format (UTF-8 string, comma-separated):
///   "close,oppose,spread"   e.g. "0.72,0.45,0.10"
///   All values are absolute, normalised to [0,1].
///
/// Thread-safe: background thread writes to volatile fields, main thread reads.
/// Stale detection: if no packet arrives within staleness_ms the data is ignored
/// and Heuristic() falls back to keyboard state.
///
/// Wire-up: drop on AgentRoot (same GameObject as DexterousPickPlaceAgent),
/// then assign the UdpSynergyReceiver reference in the agent's Inspector slot.
/// </summary>
public class UdpSynergyReceiver : MonoBehaviour
{
    [Header("Network")]
    [Tooltip("UDP port that Python mediapipe_sender.py sends to.")]
    public int port = 5005;

    [Tooltip("Milliseconds without a packet before data is considered stale.")]
    public float staleAfterMs = 500f;

    // ------------------------------------------------------------------ //
    //  Public API — read from Heuristic()
    // ------------------------------------------------------------------ //

    /// <summary>Thumb flexion [0=open, 1=closed].</summary>
    public float ThumbFlex => _thumbFlex;
    /// <summary>Index finger flexion [0=open, 1=closed].</summary>
    public float IndexFlex => _indexFlex;
    /// <summary>Middle finger flexion [0=open, 1=closed].</summary>
    public float MiddleFlex => _middleFlex;
    /// <summary>Ring finger flexion [0=open, 1=closed].</summary>
    public float RingFlex => _ringFlex;
    /// <summary>Pinky finger flexion [0=open, 1=closed].</summary>
    public float PinkyFlex => _pinkyFlex;
    /// <summary>Finger spread [0=together, 1=spread].</summary>
    public float FingerSpread => _fingerSpread;
    /// <summary>Wrist roll / palm rotation [0=pronated/palm-down, 1=supinated/palm-up, 0.5=neutral].</summary>
    public float WristRoll => _wristRoll;

    /// <summary>Backward-compat: average of 4 finger flexions.</summary>
    public float HandClose => (_indexFlex + _middleFlex + _ringFlex + _pinkyFlex) * 0.25f;
    /// <summary>Backward-compat: thumb flex value.</summary>
    public float ThumbOppose => _thumbFlex;

    /// <summary>Total UDP packets received since OnEnable. Useful for diagnosing connectivity.</summary>
    public int PacketsReceived => _packetsReceived;

    /// <summary>
    /// True when a packet arrived within the last <see cref="staleAfterMs"/> ms.
    /// Use this in Heuristic() to decide whether to trust the UDP values.
    /// </summary>
    public bool HasFreshData => _lastPacketTick != 0 && (int)(Environment.TickCount - _lastPacketTick) < (int)staleAfterMs;

    // ------------------------------------------------------------------ //
    //  Internal state
    // ------------------------------------------------------------------ //

    volatile float _thumbFlex = 0f;
    volatile float _indexFlex = 0f;
    volatile float _middleFlex = 0f;
    volatile float _ringFlex = 0f;
    volatile float _pinkyFlex = 0f;
    volatile float _fingerSpread = 0f;
    volatile float _wristRoll = 0.5f;  // neutral

    // Written by background thread; read on main thread via HasFreshData.
    // Uses Environment.TickCount (ms, thread-safe) instead of Time.realtimeSinceStartup.
    volatile int _lastPacketTick = 0;
    volatile int _packetsReceived = 0;

    UdpClient _udpClient;
    Thread _receiveThread;
    bool _running;

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //

    void OnEnable()
    {
        try
        {
            _udpClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
            _udpClient.Client.ReceiveTimeout = 200; // ms — unblocks thread on stop
        }
        catch (SocketException ex)
        {
            Debug.LogError($"[UdpSynergyReceiver] Could not bind to port {port}: {ex.Message}");
            return;
        }

        _running = true;
        _receiveThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "UdpSynergyReceiver" };
        _receiveThread.Start();

        Debug.Log($"[UdpSynergyReceiver] Listening on UDP port {port}");
    }

    void OnDisable()
    {
        _running = false;
        try { _udpClient?.Close(); } catch { /* ignored */ }
        _receiveThread?.Join(500);
        _udpClient = null;
    }

    // ------------------------------------------------------------------ //
    //  Background receive loop
    // ------------------------------------------------------------------ //

    void ReceiveLoop()
    {
        var remoteEP = new IPEndPoint(IPAddress.Any, 0);

        while (_running)
        {
            try
            {
                byte[] data = _udpClient.Receive(ref remoteEP);
                string msg = Encoding.UTF8.GetString(data).Trim();

                if (TryParse(msg, out float t, out float i, out float m, out float r, out float p, out float s, out float w))
                {
                    _thumbFlex = t;
                    _indexFlex = i;
                    _middleFlex = m;
                    _ringFlex = r;
                    _pinkyFlex = p;
                    _fingerSpread = s;
                    _wristRoll = w;
                    _lastPacketTick = Environment.TickCount != 0 ? Environment.TickCount : 1;
                    _packetsReceived++;
                }
                else
                {
                    Debug.LogWarning($"[UdpSynergyReceiver] Bad packet: '{msg}'");
                }
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut
                                              || ex.SocketErrorCode == SocketError.Interrupted)
            {
                // TimedOut  — normal 200 ms poll; loop and recheck _running.
                // Interrupted (WSACancelBlockingCall) — Windows fires this when the
                // socket is closed by OnDisable() while Receive() is blocking; treat
                // it as a clean shutdown signal rather than an error.
                if (!_running) break;
            }
            catch (ObjectDisposedException)
            {
                break; // Socket closed by OnDisable
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UdpSynergyReceiver] Receive error: {ex.Message}");
            }
        }
    }

    // ------------------------------------------------------------------ //
    //  Packet parser
    // ------------------------------------------------------------------ //

    static bool TryParse(string msg, out float thumb, out float index, out float middle,
                         out float ring, out float pinky, out float spread, out float wristRoll)
    {
        thumb = index = middle = ring = pinky = spread = 0f;
        wristRoll = 0.5f;

        string[] parts = msg.Split(',');
        if (parts.Length != 7) return false;

        return float.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                              System.Globalization.CultureInfo.InvariantCulture, out thumb)
            && float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                              System.Globalization.CultureInfo.InvariantCulture, out index)
            && float.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                              System.Globalization.CultureInfo.InvariantCulture, out middle)
            && float.TryParse(parts[3], System.Globalization.NumberStyles.Float,
                              System.Globalization.CultureInfo.InvariantCulture, out ring)
            && float.TryParse(parts[4], System.Globalization.NumberStyles.Float,
                              System.Globalization.CultureInfo.InvariantCulture, out pinky)
            && float.TryParse(parts[5], System.Globalization.NumberStyles.Float,
                              System.Globalization.CultureInfo.InvariantCulture, out spread)
            && float.TryParse(parts[6], System.Globalization.NumberStyles.Float,
                              System.Globalization.CultureInfo.InvariantCulture, out wristRoll);
    }
}
