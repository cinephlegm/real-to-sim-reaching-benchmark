"""
mediapipe_sender.py
-------------------
Captures right-hand landmarks from a Kinect v2 colour stream (or webcam fallback)
using the MediaPipe Hand Landmarker, derives per-finger control values plus spread
and wrist roll, smooths them, and sends them to Unity's UdpSynergyReceiver.

Packet format:
  "thumb,index,middle,ring,pinky,spread,roll"
  - values are normalised to [0, 1]
  - default target: 127.0.0.1:5005

Usage:
  python mediapipe_sender.py [--host HOST] [--port PORT]
                             [--camera CAM_INDEX] [--mirror]
                             [--alpha ALPHA] [--no-kinect]

Requirements:
  pip install mediapipe opencv-python pykinect2

Controls:
  Q / ESC  — quit
"""

import argparse
import math
import os
import socket
import sys
import time
import urllib.request

import cv2
import numpy as np
import mediapipe as mp
from mediapipe.tasks import python as mp_python
from mediapipe.tasks.python import vision as mp_vision

# ---------------------------------------------------------------------------
# Model download
# ---------------------------------------------------------------------------
MODEL_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "hand_landmarker.task")
MODEL_URL  = ("https://storage.googleapis.com/mediapipe-models/"
              "hand_landmarker/hand_landmarker/float16/1/hand_landmarker.task")

def ensure_model():
    if not os.path.exists(MODEL_PATH):
        print("[mediapipe_sender] Downloading hand_landmarker.task model (~8 MB)…")
        urllib.request.urlretrieve(MODEL_URL, MODEL_PATH)
        print("[mediapipe_sender] Model saved to", MODEL_PATH)

# ---------------------------------------------------------------------------
# Hand connection pairs for manual drawing (MediaPipe 21-point hand model)
# ---------------------------------------------------------------------------
HAND_CONNECTIONS = [
    (0,1),(1,2),(2,3),(3,4),
    (0,5),(5,6),(6,7),(7,8),
    (5,9),(9,10),(10,11),(11,12),
    (9,13),(13,14),(14,15),(15,16),
    (13,17),(17,18),(18,19),(19,20),
    (0,17),
]


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def parse_args():
    p = argparse.ArgumentParser(description="MediaPipe hand synergy → Unity UDP")
    p.add_argument("--host",   default="127.0.0.1", help="UDP target host (default: 127.0.0.1)")
    p.add_argument("--port",   type=int, default=5005, help="UDP target port (default: 5005)")
    p.add_argument("--camera", type=int, default=0,    help="Webcam index for fallback (default: 0)")
    p.add_argument("--mirror", action="store_true",    help="Flip frame horizontally (selfie mode)")
    p.add_argument("--alpha",  type=float, default=0.15,
                   help="Low-pass smoothing factor 0–1, lower = smoother (default: 0.15)")
    p.add_argument("--no-kinect", action="store_true",
                   help="Skip Kinect and use regular webcam")
    return p.parse_args()


# ---------------------------------------------------------------------------
# Landmark indices (MediaPipe 21-point hand model)
# ---------------------------------------------------------------------------
# Wrist: 0
# Thumb:  1-4  (CMC, MCP, IP, TIP)
# Index:  5-8  (MCP, PIP, DIP, TIP)
# Middle: 9-12
# Ring:  13-16
# Pinky: 17-20

WRIST       = 0
THUMB_CMC   = 1;  THUMB_MCP   = 2;  THUMB_IP    = 3;  THUMB_TIP   = 4
INDEX_MCP   = 5;  INDEX_PIP   = 6;  INDEX_DIP   = 7;  INDEX_TIP   = 8
MIDDLE_MCP  = 9;  MIDDLE_PIP  = 10; MIDDLE_DIP  = 11; MIDDLE_TIP  = 12
RING_MCP    = 13; RING_PIP    = 14; RING_DIP    = 15; RING_TIP    = 16
PINKY_MCP   = 17; PINKY_PIP   = 18; PINKY_DIP   = 19; PINKY_TIP   = 20


# ---------------------------------------------------------------------------
# Per-finger dexterous computation
# ---------------------------------------------------------------------------

def _vec3(a, b):
    return (b.x - a.x, b.y - a.y, b.z - a.z)


def _norm3(v):
    mag = math.sqrt(sum(x * x for x in v)) + 1e-6
    return tuple(x / mag for x in v)


def _angle_at(lm, a_idx, vertex_idx, b_idx):
    """Interior angle in radians at vertex_idx (the middle joint).
    Returns π when the 3 points are collinear (straight), decreasing as they bend."""
    va = _norm3(_vec3(lm[vertex_idx], lm[a_idx]))
    vb = _norm3(_vec3(lm[vertex_idx], lm[b_idx]))
    dot = max(-1.0, min(1.0, sum(x * y for x, y in zip(va, vb))))
    return math.acos(dot)


def _finger_curl(lm, mcp_idx, pip_idx, dip_idx, tip_idx):
    """Per-finger curl using sum of joint bends at MCP + PIP + DIP.
    bend = π − joint_angle  (0 when straight, positive when curled).
    Normalised by ~195° — the realistic total bend MediaPipe measures for a
    tight fist (self-occlusion causes landmarks to underestimate distal joints
    so the true 260° theoretical max is never reached in practice).
    Returns 0 = fully open, 1 = fully closed.
    """
    angle_mcp = _angle_at(lm, WRIST,   mcp_idx, pip_idx)
    angle_pip = _angle_at(lm, mcp_idx, pip_idx, dip_idx)
    angle_dip = _angle_at(lm, pip_idx, dip_idx, tip_idx)
    total_bend = (math.pi - angle_mcp) + (math.pi - angle_pip) + (math.pi - angle_dip)
    return min(max(total_bend / math.radians(195.0), 0.0), 1.0)


def _thumb_curl(lm):
    """Thumb closure using MCP + IP joint flexion angles ONLY.

    The WRIST-CMC-MCP triplet is deliberately excluded: the thumb sticks out
    sideways so those three points are never collinear even in a fully open
    hand.  Including it creates a large baseline offset that compresses the
    usable range and can make a resting open hand report ~0.3-0.5 curl.

    MCP and IP are both reliable: they start near π (straight) and sweep
    ~45-80° each when the thumb closes.  Total max ≈ 140° but MediaPipe
    under-estimates under self-occlusion, so we normalise by 85° so that a
    typical tight closure reaches 1.0 without clipping light curls.

    Returns 0 = fully open/extended, 1 = fully closed/opposed.
    """
    angle_mcp = _angle_at(lm, THUMB_CMC, THUMB_MCP, THUMB_IP)
    angle_ip  = _angle_at(lm, THUMB_MCP, THUMB_IP,  THUMB_TIP)
    total_bend = (math.pi - angle_mcp) + (math.pi - angle_ip)
    return min(max(total_bend / math.radians(85.0), 0.0), 1.0)


def _wrist_roll(lm):
    """Palm orientation using ONLY 2D image-plane coordinates (x, y).
    MediaPipe z is too noisy to use in a cross product — this uses the
    signed area of the triangle wrist→index_MCP→pinky_MCP in screen space.
    Positive = palm facing camera (right hand supinated toward screen).
    0=back-of-hand, 0.5=edge-on, 1=palm-facing.
    """
    # purely 2D — z component of the 2D cross product
    v1x = lm[INDEX_MCP].x - lm[WRIST].x
    v1y = lm[INDEX_MCP].y - lm[WRIST].y
    v2x = lm[PINKY_MCP].x - lm[WRIST].x
    v2y = lm[PINKY_MCP].y - lm[WRIST].y
    nz = v1x * v2y - v1y * v2x   # > 0: palm toward camera, < 0: back toward camera
    # Typical magnitude ~0.02–0.08 for a flat hand; scale to fill [0, 1] well
    return (max(-1.0, min(1.0, nz * 15.0)) + 1.0) * 0.5


def compute_fingers(lm):
    """
    Derive six per-finger values in [0, 1] from MediaPipe landmarks.

    Parameters
    ----------
    lm : list[NormalizedLandmark]  — 21 landmarks from a single detected hand.

    Returns
    -------
    thumb, index, middle, ring, pinky, spread : float  (each in [0, 1])
    """
    thumb  = _thumb_curl(lm)
    index  = _finger_curl(lm, INDEX_MCP,  INDEX_PIP,  INDEX_DIP,  INDEX_TIP)
    middle = _finger_curl(lm, MIDDLE_MCP, MIDDLE_PIP, MIDDLE_DIP, MIDDLE_TIP)
    ring   = _finger_curl(lm, RING_MCP,   RING_PIP,   RING_DIP,   RING_TIP)
    pinky  = _finger_curl(lm, PINKY_MCP,  PINKY_PIP,  PINKY_DIP,  PINKY_TIP)

    # Spread: angular gap between index-MCP and pinky-MCP vectors from wrist
    v_idx = _norm3(_vec3(lm[WRIST], lm[INDEX_MCP]))
    v_pnk = _norm3(_vec3(lm[WRIST], lm[PINKY_MCP]))
    dot   = sum(a * b for a, b in zip(v_idx, v_pnk))
    angle = math.acos(min(max(dot, -1.0), 1.0))
    spread = min(angle / math.radians(45.0), 1.0)

    roll = _wrist_roll(lm)

    return thumb, index, middle, ring, pinky, spread, roll


# ---------------------------------------------------------------------------
# Low-pass (exponential) smoother
# ---------------------------------------------------------------------------

class EMAFilter:
    def __init__(self, alpha, n):
        self._alpha = alpha
        self._state = [0.0] * n

    def update(self, values):
        a = self._alpha
        self._state = [a * v + (1.0 - a) * s for v, s in zip(values, self._state)]
        return list(self._state)


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def _try_open_kinect():
    """Try to open Kinect v2 colour stream via pykinect2. Returns runtime or None."""
    try:
        from pykinect2 import PyKinectV2, PyKinectRuntime
        kinect = PyKinectRuntime.PyKinectRuntime(PyKinectV2.FrameSourceTypes_Color)
        print("[mediapipe_sender] Kinect v2 opened — using colour stream.")
        return kinect
    except Exception as e:
        print(f"[mediapipe_sender] Kinect init failed ({e}), falling back to webcam.")
        return None


def _kinect_frame(kinect):
    """Read latest colour frame from Kinect as a 1920x1080 BGR numpy array, or None."""
    if kinect.has_new_color_frame():
        frame = kinect.get_last_color_frame()
        img = frame.reshape((1080, 1920, 4)).astype(np.uint8)
        return img[:, :, :3]   # BGRA → BGR
    return None


def _draw_landmarks(frame, landmarks, h, w):
    """Draw hand skeleton on frame using cv2 (replaces mp_draw)."""
    frame = np.ascontiguousarray(frame)
    pts = [(int(lm.x * w), int(lm.y * h)) for lm in landmarks]
    for a, b in HAND_CONNECTIONS:
        cv2.line(frame, pts[a], pts[b], (0, 200, 0), 2)
    for pt in pts:
        cv2.circle(frame, pt, 5, (0, 255, 0), -1)
    return frame


def main():
    args = parse_args()

    ensure_model()

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    target = (args.host, args.port)

    smoother = EMAFilter(alpha=args.alpha, n=7)

    # --- Camera source: Kinect v2 preferred, webcam fallback ---
    kinect = None
    cap    = None

    if not getattr(args, 'no_kinect', False):
        kinect = _try_open_kinect()

    if kinect is None:
        cap = cv2.VideoCapture(args.camera, cv2.CAP_DSHOW)
        if not cap.isOpened():
            cap = cv2.VideoCapture(args.camera)
        if not cap.isOpened():
            print(f"[ERROR] Cannot open camera {args.camera}", file=sys.stderr)
            sys.exit(1)
        print(f"[mediapipe_sender] Using webcam index {args.camera}")

    print(f"[mediapipe_sender] Sending to udp://{args.host}:{args.port}")
    print("[mediapipe_sender] Press Q or ESC to quit.")

    base_options = mp_python.BaseOptions(model_asset_path=MODEL_PATH)
    # num_hands=2 so MediaPipe finds both hands and we can filter by handedness.
    # We only USE the right hand (see filtering loop below).
    options = mp_vision.HandLandmarkerOptions(
        base_options=base_options,
        running_mode=mp_vision.RunningMode.VIDEO,
        num_hands=2,
        min_hand_detection_confidence=0.3,
        min_hand_presence_confidence=0.3,
        min_tracking_confidence=0.3,
    )

    with mp_vision.HandLandmarker.create_from_options(options) as landmarker:
        prev_time  = time.perf_counter()
        start_time = prev_time

        while True:
            # --- Grab frame ---
            frame = None
            if kinect is not None:
                frame = _kinect_frame(kinect)
                if frame is None:
                    time.sleep(0.005)
                    continue
            else:
                ret, frame = cap.read()
                if not ret or frame is None:
                    print("[WARNING] Empty frame, retrying…")
                    continue

            if args.mirror:
                frame = cv2.flip(frame, 1)

            h, w = frame.shape[:2]

            # Downscale to 640×360 for MediaPipe (faster + better detection than raw 1080p)
            mp_frame = cv2.resize(frame, (640, 360)) if w > 640 else frame
            rgb = cv2.cvtColor(mp_frame, cv2.COLOR_BGR2RGB)
            mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb)
            timestamp_ms = int((time.perf_counter() - start_time) * 1000)
            result = landmarker.detect_for_video(mp_image, timestamp_ms)

            thumb, index, middle, ring, pinky, spread = 0.0, 0.0, 0.0, 0.0, 0.0, 0.0
            hand_visible = False
            roll = 0.5  # neutral when no hand

            if result.hand_landmarks:
                # Only use the user's physical RIGHT hand.
                # With --mirror the image is flipped before MediaPipe sees it, so
                # MediaPipe labels the right hand as "Left" (anatomy is mirrored).
                target_label = "Left" if args.mirror else "Right"
                selected_lm = None
                for i, lm_candidate in enumerate(result.hand_landmarks):
                    if i < len(result.handedness) and result.handedness[i]:
                        label = result.handedness[i][0].category_name
                        if label == target_label:
                            selected_lm = lm_candidate
                            break
                # No fallback — if right hand not visible, send zeros.
                if selected_lm is not None:
                    lm = selected_lm
                    raw = compute_fingers(lm)
                    thumb, index, middle, ring, pinky, spread, roll = smoother.update(list(raw))
                    hand_visible = True
                    frame = _draw_landmarks(frame, lm, h, w)

            # Only send when a valid right hand is tracked.
            # When no hand is visible, send nothing — Unity's UdpSynergyReceiver
            # marks data stale after 500 ms and the hand driver holds its last pose,
            # rather than snapping to zeros and violently opening/closing.
            if hand_visible:
                msg = f"{thumb:.4f},{index:.4f},{middle:.4f},{ring:.4f},{pinky:.4f},{spread:.4f},{roll:.4f}"
                sock.sendto(msg.encode("utf-8"), target)

            # --- HUD overlay (scale down Kinect 1080p for display) ---
            frame = np.ascontiguousarray(frame)
            display = cv2.resize(frame, (960, 540)) if w > 1280 else frame

            now  = time.perf_counter()
            fps  = 1.0 / max(now - prev_time, 1e-6)
            prev_time = now

            status = "TRACKING" if hand_visible else "NO HAND"
            color  = (0, 220, 0) if hand_visible else (0, 60, 220)
            cv2.putText(display, f"{status}  FPS:{fps:.1f}", (10, 28),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.8, color, 2)
            cv2.putText(display, f"T={thumb:.2f} I={index:.2f} M={middle:.2f} R={ring:.2f} P={pinky:.2f} Sp={spread:.2f} Wr={roll:.2f}",
                        (10, 60), cv2.FONT_HERSHEY_SIMPLEX, 0.58, (220, 220, 0), 2)
            cv2.putText(display, f"-> udp://{args.host}:{args.port}", (10, 90),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.55, (180, 180, 180), 1)

            cv2.imshow("MediaPipe Hand Sender", display)
            key = cv2.waitKey(1) & 0xFF
            if key in (ord("q"), ord("Q"), 27):  # Q or ESC
                break

    if cap is not None:
        cap.release()
    if kinect is not None:
        kinect.close()
    cv2.destroyAllWindows()
    sock.close()
    print("[mediapipe_sender] Stopped.")


if __name__ == "__main__":
    main()
