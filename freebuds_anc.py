"""
Huawei FreeBuds SE4 ANC Controller
====================================
Tidak butuh library eksternal — pakai socket bawaan Python.

Cara pakai:
  python freebuds_anc.py scan              -> cari MAC address FreeBuds
  python freebuds_anc.py status            -> cek mode ANC yang sedang aktif
  python freebuds_anc.py off               -> matikan ANC (Off)
  python freebuds_anc.py awareness        -> Awareness / Transparency mode
  python freebuds_anc.py anc              -> Noise Cancelling (ANC On)
  python freebuds_anc.py anc general      -> Noise Cancelling mode General
  python freebuds_anc.py anc cozy         -> Noise Cancelling mode Cozy (ringan)
  python freebuds_anc.py anc ultra        -> Noise Cancelling mode Ultra (paling kuat)
  python freebuds_anc.py anc auto         -> Noise Cancelling mode Dynamic/Auto
  python freebuds_anc.py gui              -> buka GUI window (butuh tkinter)
  python freebuds_anc.py help             -> bantuan ini

Tidak perlu pip install apapun!
"""

import sys
import socket
import time
import platform

# Pastikan output console di Windows tidak crash saat mencetak karakter khusus/emoji
if sys.platform == "win32":
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

# ============================================================
# KONFIGURASI — ganti dengan MAC address FreeBuds kamu!
# Jalankan: python freebuds_anc.py scan
# ============================================================
DEVICE_MAC = "0C:17:73:61:85:E6"

# ============================================================
# Protokol Huawei FreeBuds (reverse-engineered, via SPP)
# ============================================================
CMD_SET_ANC_MODE = b"\x2b\x04"
CMD_GET_ANC_MODE = b"\x2b\x2a"
CMD_GET_ANC_PREF = b"\x2b\x19"

# Mode mapping:
# 0x00: Off
# 0x01: Noise Cancelling (ANC)
# 0x02: Awareness (Transparency)
ANC_MODE_MAP = {
    "off":              0x00,
    "anc":              0x01,
    "noise_cancelling": 0x01,
    "cancelling":       0x01,
    "awareness":        0x02,
    "transparency":     0x02,
    "transparan":       0x02,
}

# Level mapping untuk Noise Cancelling (Mode 0x01):
# 0x00: General / Normal
# 0x01: Cozy / Comfort
# 0x02: Ultra
# 0x03: Dynamic / Auto
ANC_LEVEL_MAP = {
    "general": 0x00,
    "normal":  0x00,
    "cozy":    0x01,
    "comfort": 0x01,
    "ultra":   0x02,
    "auto":    0x03,
    "dynamic": 0x03,
}

ANC_MODE_NAME  = {0x00: "Off", 0x01: "Noise Cancelling", 0x02: "Awareness"}
ANC_LEVEL_NAME = {0x00: "General", 0x01: "Cozy", 0x02: "Ultra", 0x03: "Dynamic"}


# ─── CRC16-XModem ────────────────────────────────────────────
_CRC_TAB = [
    0, 4129, 8258, 12387, 16516, 20645, 24774, 28903,
    -32504, -28375, -24246, -20117, -15988, -11859, -7730, -3601,
    4657, 528, 12915, 8786, 21173, 17044, 29431, 25302,
    -27847, -31976, -19589, -23718, -11331, -15460, -3073, -7202,
    9314, 13379, 1056, 5121, 25830, 29895, 17572, 21637,
    -23190, -19125, -31448, -27383, -6674, -2609, -14932, -10867,
    13907, 9842, 5649, 1584, 30423, 26358, 22165, 18100,
    -18597, -22662, -26855, -30920, -2081, -6146, -10339, -14404,
    18628, 22757, 26758, 30887, 2112, 6241, 10242, 14371,
    -13876, -9747, -5746, -1617, -30392, -26263, -22262, -18133,
    23285, 19156, 31415, 27286, 6769, 2640, 14899, 10770,
    -9219, -13348, -1089, -5218, -25735, -29864, -17605, -21734,
    27814, 31879, 19684, 23749, 11298, 15363, 3168, 7233,
    -4690, -625, -12820, -8755, -21206, -17141, -29336, -25271,
    32407, 28342, 24277, 20212, 15891, 11826, 7761, 3696,
    -97, -4162, -8227, -12292, -16613, -20678, -24743, -28808,
    -28280, -32343, -20022, -24085, -12020, -16083, -3762, -7825,
    4224, 161, 12482, 8419, 20484, 16421, 28742, 24679,
    -31815, -27752, -23557, -19494, -15555, -11492, -7297, -3234,
    689, 4752, 8947, 13010, 16949, 21012, 25207, 29270,
    -18966, -23093, -27224, -31351, -2706, -6833, -10964, -15091,
    13538, 9411, 5280, 1153, 29798, 25671, 21540, 17413,
    -22565, -18438, -30823, -26696, -6305, -2178, -14563, -10436,
    9939, 14066, 1681, 5808, 26199, 30326, 17941, 22068,
    -9908, -13971, -1778, -5841, -26168, -30231, -18038, -22101,
    22596, 18533, 30726, 26663, 6336, 2273, 14466, 10403,
    -13443, -9380, -5313, -1250, -29703, -25640, -21573, -17510,
    19061, 23124, 27191, 31254, 2801, 6864, 10931, 14994,
    -722, -4849, -8852, -12979, -16982, -21109, -25112, -29239,
    31782, 27655, 23652, 19525, 15522, 11395, 7392, 3265,
    -4321, -194, -12451, -8324, -20581, -16454, -28711, -24584,
    28183, 32310, 20053, 24180, 11923, 16050, 3793, 7920,
]

def crc16(data: bytes) -> bytes:
    s = 0
    for b in data:
        s = _CRC_TAB[((s >> 8) ^ b) & 255] ^ (s << 8)
        s &= 0xFFFF
    return s.to_bytes(2, "big")


def build_packet(cmd: bytes, params: list) -> bytes:
    body = cmd
    for p_type, p_val in params:
        if isinstance(p_val, int):
            p_val = p_val.to_bytes(1, "big")
        body += bytes([p_type, len(p_val)]) + p_val
    header = b"\x5a" + (len(body) + 1).to_bytes(2, "big") + b"\x00"
    pkt = header + body
    return pkt + crc16(pkt)


# ─── Windows: cari port RFCOMM dari registry ─────────────────
def _win_rfcomm_port(mac: str) -> int | None:
    """
    Cari port COM / RFCOMM channel untuk device dari Windows registry.
    Tidak butuh library eksternal.
    """
    try:
        import winreg
        # Normalisasi MAC: ganti - dengan _
        mac_key = mac.upper().replace(":", "_").replace("-", "_")
        base = r"SYSTEM\CurrentControlSet\Services\BTHPORT\Parameters\Devices"
        with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, base) as root:
            for i in range(winreg.QueryInfoKey(root)[0]):
                subkey_name = winreg.EnumKey(root, i)
                if subkey_name.upper().replace("-", "_") == mac_key.replace(":", "_"):
                    return None  # lanjut ke port scan
    except Exception:
        pass
    return None


def _scan_rfcomm_port(mac: str, ports=(1, 15, 16, 2, 3, 4, 5, 17, 18)) -> int | None:
    """Scan port RFCOMM yang mau menerima koneksi."""
    print(f"    Mencari port RFCOMM di {mac}...")
    for port in ports:
        try:
            s = socket.socket(socket.AF_BLUETOOTH, socket.SOCK_STREAM, socket.BTPROTO_RFCOMM)
            s.settimeout(2)
            s.connect((mac, port))
            s.close()
            print(f"    Port ditemukan: {port}")
            return port
        except (ConnectionRefusedError, OSError):
            continue
        except Exception:
            continue
    return None


# ─── Koneksi ─────────────────────────────────────────────────
def connect(mac: str, port: int = None, timeout: int = 4):
    """Buka RFCOMM socket ke FreeBuds (FreeBuds SE4 ANC biasanya di port 1)."""
    try_ports = [port] if port else [1, 15, 16, 2, 3, 4, 5, 17, 18]
    for p in try_ports:
        try:
            s = socket.socket(socket.AF_BLUETOOTH, socket.SOCK_STREAM, socket.BTPROTO_RFCOMM)
            s.settimeout(timeout)
            s.connect((mac, p))
            print(f"[OK] Terhubung ke {mac} (port {p})")
            return s, p
        except ConnectionRefusedError:
            continue
        except OSError as e:
            err = str(e).lower()
            if "host is down" in err or "no route" in err or "unreachable" in err:
                print(f"[!] FreeBuds tidak ditemukan / tidak nyala di {mac}")
                print("    Pastikan:")
                print("    1. FreeBuds sedang dipakai / tidak di dalam case")
                print("    2. FreeBuds sudah di-pair ke laptop ini")
                print("    3. Bluetooth laptop menyala")
                return None, None
            continue
        except Exception:
            continue
    print(f"[!] Semua port RFCOMM ditolak untuk {mac}")
    print("    FreeBuds mungkin sibuk dengan koneksi dari HP.")
    print("    Coba: putus koneksi HP dulu, baru jalankan lagi.")
    return None, None


# ─── Set ANC Mode ─────────────────────────────────────────────
def set_anc(mac: str, mode: str, level: str = None):
    mode_clean = mode.lower().replace("-", "_").replace(" ", "_")
    if mode_clean not in ANC_MODE_MAP:
        print(f"[!] Mode tidak valid: '{mode}'")
        print("    Pilihan mode: off, awareness (transparency), anc (noise cancelling)")
        return False

    mode_val = ANC_MODE_MAP[mode_clean]

    # Pada protokol Huawei FreeBuds:
    # Parameter 1 bernilai 2 bytes: [mode, level]
    # - Mode 0 (Off): [0x00, 0x00]
    # - Mode 2 (Awareness): [0x02, 0xFF]
    # - Mode 1 (Noise Cancelling): [0x01, level] atau [0x01, 0xFF] (default)
    if mode_val == 0x00:
        payload = b"\x00\x00"
        label = "Off"
    elif mode_val == 0x02:
        payload = b"\x02\xff"
        label = "Awareness"
    else:
        if level:
            lvl_clean = level.lower()
            lvl_val = ANC_LEVEL_MAP.get(lvl_clean, 0x00)
            payload = bytes([0x01, lvl_val])
            label = f"Noise Cancelling — {ANC_LEVEL_NAME.get(lvl_val, level.title())}"
        else:
            payload = b"\x01\xff"
            label = "Noise Cancelling"

    pkt = build_packet(CMD_SET_ANC_MODE, [(1, payload)])

    print(f"[->] Mengirim perintah: {label}")
    print(f"    Hex: {pkt.hex()}")

    sock, _ = connect(mac)
    if not sock:
        return False

    try:
        sock.send(pkt)
        time.sleep(0.3)

        # Baca respons konfirmasi dari earbuds
        sock.settimeout(1.0)
        try:
            resp = sock.recv(256)
            if resp:
                print(f"[<-] ACK/Respons: {resp.hex()}")
        except socket.timeout:
            pass

        print(f"\n[OK] Berhasil diatur ke: {label}")
        return True

    except Exception as e:
        print(f"[!] Error saat kirim: {e}")
        return False
    finally:
        sock.close()


# ─── Get ANC Status ───────────────────────────────────────────
def get_anc_status(mac: str):
    """Membaca status mode ANC yang sedang aktif di FreeBuds."""
    print(f"[*] Mengecek status ANC FreeBuds ({mac})...")
    sock, _ = connect(mac)
    if not sock:
        return None

    try:
        pkt = build_packet(CMD_GET_ANC_MODE, [(1, b""), (2, b"")])
        sock.send(pkt)
        time.sleep(0.3)

        sock.settimeout(1.5)
        resp = sock.recv(1024)
        if not resp:
            print("[!] Tidak ada balasan dari FreeBuds.")
            return None

        idx = resp.find(CMD_GET_ANC_MODE)
        if idx != -1 and idx + 5 < len(resp):
            lvl_val = resp[idx + 4]
            mode_val = resp[idx + 5]

            mode_str = ANC_MODE_NAME.get(mode_val, f"Unknown ({mode_val})")
            if mode_val == 0x01:
                lvl_str = ANC_LEVEL_NAME.get(lvl_val, f"Level {lvl_val}")
                full_status = f"{mode_str} ({lvl_str})"
            else:
                full_status = mode_str

            print(f"\n[OK] Status saat ini: {full_status}")
            return {
                "mode": mode_val,
                "mode_str": mode_str,
                "level": lvl_val,
                "level_str": ANC_LEVEL_NAME.get(lvl_val, str(lvl_val)),
                "full_status": full_status,
            }

        print("[!] Format status tidak dikenali.")
        return None
    except Exception as e:
        print(f"[!] Error saat cek status: {e}")
        return None
    finally:
        sock.close()


# ─── Scan perangkat Bluetooth (Windows) ──────────────────────
def scan_windows():
    """Scan via Windows devcon / PowerShell — tidak butuh library."""
    import subprocess
    print("[*] Mencari perangkat Bluetooth yang sudah di-pair...\n")

    # Coba PowerShell (Windows 8+)
    ps_cmd = (
        "Get-PnpDevice -Class Bluetooth | "
        "Select-Object FriendlyName, DeviceID | "
        "Format-Table -AutoSize"
    )
    try:
        result = subprocess.run(
            ["powershell", "-NoProfile", "-Command", ps_cmd],
            capture_output=True, text=True, timeout=10
        )
        if result.returncode == 0 and result.stdout.strip():
            print(result.stdout)
            print("Cari baris yang mengandung 'HUAWEI' atau 'FreeBuds'.")
            print("MAC address ada di kolom DeviceID, format: BTHENUM\\DEV_XXXXXXXXXXXX")
            print("Ambil 12 karakter terakhir lalu format jadi XX:XX:XX:XX:XX:XX\n")
            return
    except Exception:
        pass

    # Fallback: coba WMI
    try:
        result = subprocess.run(
            ["wmic", "path", "Win32_PnPEntity",
             "where", "PNPClass='Bluetooth'",
             "get", "Name,DeviceID"],
            capture_output=True, text=True, timeout=10
        )
        if result.returncode == 0 and result.stdout.strip():
            print(result.stdout[:2000])
            return
    except Exception:
        pass

    print("[!] Tidak bisa scan otomatis.")
    print_mac_help()


def print_mac_help():
    print("""
Cara cari MAC address FreeBuds secara manual:

Windows 10/11:
  1. Start → Settings → Bluetooth & devices
  2. Klik nama FreeBuds SE4
  3. Cari 'More info' atau 'Properties'
  4. Lihat bagian 'Bluetooth address' atau 'Device address'

  Atau buka Device Manager:
  1. Tekan Win+X → Device Manager
  2. Bluetooth → cari HUAWEI FreeBuds
  3. Klik kanan → Properties → Details → Hardware IDs
  4. Cari angka setelah 'DEV_' = MAC (format hex, 12 karakter)
     Contoh: DEV_AC67B2AABBCC → MAC = AC:67:B2:AA:BB:CC
""")


def scan():
    system = platform.system()
    if system == "Windows":
        scan_windows()
    elif system == "Linux":
        import subprocess
        try:
            result = subprocess.run(
                ["bluetoothctl", "devices"],
                capture_output=True, text=True, timeout=5
            )
            print(result.stdout or "[!] Tidak ada perangkat. Coba pair dulu.")
        except FileNotFoundError:
            print("[!] bluetoothctl tidak ditemukan.")
            print("    Coba: hcitool scan")
    else:
        print_mac_help()


# ─── GUI (tkinter) ────────────────────────────────────────────
def gui():
    try:
        import tkinter as tk
    except ImportError:
        print("[!] tkinter tidak tersedia.")
        print("    Pakai command line saja: python freebuds_anc.py anc")
        return

    root = tk.Tk()
    root.title("FreeBuds SE4 — ANC Controller")
    root.geometry("380x470")
    root.resizable(False, False)
    root.configure(bg="#111827")

    # ── Header ──
    tk.Label(root, text="🎧", font=("Segoe UI Emoji", 30),
             bg="#111827", fg="white").pack(pady=(16, 0))
    tk.Label(root, text="FreeBuds SE4 ANC", font=("Segoe UI", 15, "bold"),
             bg="#111827", fg="#f0f0ff").pack()
    tk.Label(root, text="Noise Cancellation Controller", font=("Segoe UI", 9),
             bg="#111827", fg="#6b7280").pack(pady=(0, 10))

    # ── MAC input ──
    mac_frame = tk.Frame(root, bg="#1f2937", padx=12, pady=8)
    mac_frame.pack(fill="x", padx=24)

    tk.Label(mac_frame, text="MAC Address", font=("Segoe UI", 8),
             bg="#1f2937", fg="#9ca3af").pack(anchor="w")
    mac_var = tk.StringVar(value=DEVICE_MAC)
    mac_entry = tk.Entry(mac_frame, textvariable=mac_var,
                         font=("Courier New", 12), bg="#111827", fg="#a78bfa",
                         insertbackground="#a78bfa", relief=tk.FLAT,
                         highlightthickness=1, highlightbackground="#374151",
                         width=20)
    mac_entry.pack(fill="x", pady=(4, 0))

    # ── Status ──
    status_var = tk.StringVar(value="Pilih mode ANC")
    status_lbl = tk.Label(root, textvariable=status_var,
                          font=("Segoe UI", 9, "bold"), bg="#111827", fg="#9ca3af",
                          wraplength=340)
    status_lbl.pack(pady=8)

    # ── Tombol Mode ──
    btn_data = [
        ("🔇  Off",                    "off",          None,       "#1f2937", "#9ca3af"),
        ("👂  Awareness",              "awareness",    None,       "#1e3a5f", "#60a5fa"),
        ("🔕  Noise Cancelling",       "anc",          None,       "#14532d", "#4ade80"),
        ("🔕  ANC — General",         "anc",          "general",  "#134e4a", "#2dd4bf"),
        ("🔕  ANC — Cozy",            "anc",          "cozy",     "#1e3a8a", "#93c5fd"),
        ("🔕  ANC — Ultra",           "anc",          "ultra",    "#0c4a6e", "#38bdf8"),
    ]

    def do_set(mode, level, label):
        mac = mac_var.get().strip()
        if mac == "XX:XX:XX:XX:XX:XX" or not mac:
            status_var.set("⚠️  Isi MAC address FreeBuds dulu!")
            status_lbl.config(fg="#f59e0b")
            return
        status_var.set(f"⏳ Mengirim: {label}...")
        status_lbl.config(fg="#9ca3af")
        root.update()
        ok = set_anc(mac, mode, level)
        if ok:
            status_var.set(f"✅  Aktif: {label}")
            status_lbl.config(fg="#4ade80")
        else:
            status_var.set("❌  Gagal — lihat terminal untuk detail")
            status_lbl.config(fg="#f87171")

    def do_check_status():
        mac = mac_var.get().strip()
        if mac == "XX:XX:XX:XX:XX:XX" or not mac:
            status_var.set("⚠️  Isi MAC address FreeBuds dulu!")
            status_lbl.config(fg="#f59e0b")
            return
        status_var.set("⏳ Memeriksa status dari earbuds...")
        status_lbl.config(fg="#9ca3af")
        root.update()
        res = get_anc_status(mac)
        if res:
            status_var.set(f"🎧 Status: {res['full_status']}")
            status_lbl.config(fg="#60a5fa")
        else:
            status_var.set("❌  Gagal membaca status.")
            status_lbl.config(fg="#f87171")

    btn_frame = tk.Frame(root, bg="#111827")
    btn_frame.pack(pady=2, padx=24, fill="x")

    for label, mode, level, bg, fg in btn_data:
        btn = tk.Button(
            btn_frame, text=label,
            command=lambda m=mode, lv=level, lb=label: do_set(m, lv, lb),
            bg=bg, fg=fg, activebackground="#374151", activeforeground="white",
            font=("Segoe UI", 9, "bold"), relief=tk.FLAT, cursor="hand2",
            pady=7, bd=0
        )
        btn.pack(fill="x", pady=2)
        btn.bind("<Enter>", lambda e, b=btn: b.config(bg="#374151"))
        btn.bind("<Leave>", lambda e, b=btn, c=bg: b.config(bg=c))

    # ── Action bar (Check Status & Scan) ──
    act_frame = tk.Frame(root, bg="#111827")
    act_frame.pack(pady=(8, 4))

    tk.Button(act_frame, text="🔄  Cek Status",
              command=do_check_status, bg="#111827", fg="#a78bfa",
              font=("Segoe UI", 8), relief=tk.FLAT, cursor="hand2",
              padx=8, pady=4).pack(side="left", padx=4)

    tk.Button(act_frame, text="🔍  Scan MAC",
              command=scan, bg="#111827", fg="#6b7280",
              font=("Segoe UI", 8), relief=tk.FLAT, cursor="hand2",
              padx=8, pady=4).pack(side="left", padx=4)

    root.mainloop()


# ─── Help ──────────────────────────────────────────────────────
def print_help():
    print("""
Huawei FreeBuds SE4 ANC Controller
====================================
Tidak perlu install library apapun!

Penggunaan:
  python freebuds_anc.py status            cek mode ANC saat ini
  python freebuds_anc.py off               matikan ANC (Off)
  python freebuds_anc.py awareness        mode Awareness (Transparency)
  python freebuds_anc.py anc              mode Noise Cancelling (ANC On)
  python freebuds_anc.py anc general      mode Noise Cancelling General
  python freebuds_anc.py anc cozy         mode Noise Cancelling Cozy (ringan)
  python freebuds_anc.py anc ultra        mode Noise Cancelling Ultra (paling kuat)
  python freebuds_anc.py anc auto         mode Noise Cancelling Dynamic
  python freebuds_anc.py scan              cari MAC address FreeBuds
  python freebuds_anc.py gui              buka antarmuka jendela GUI
  python freebuds_anc.py help             bantuan ini

Setup pertama kali:
  1. Pair FreeBuds ke laptop via Bluetooth Settings
  2. Jalankan: python freebuds_anc.py scan
  3. Edit DEVICE_MAC di baris atas script ini
  4. Jalankan perintah yang diinginkan
""")


# ─── Main ──────────────────────────────────────────────────────
if __name__ == "__main__":
    args = sys.argv[1:]

    if not args or args[0] in ("help", "--help", "-h"):
        print_help()
        sys.exit(0)

    cmd = args[0].lower()

    if cmd == "scan":
        scan()
        sys.exit(0)

    if cmd == "gui":
        gui()
        sys.exit(0)

    # Validasi MAC
    if DEVICE_MAC == "XX:XX:XX:XX:XX:XX":
        print("[!] MAC address belum diset!")
        print("    Edit baris DEVICE_MAC di script ini.")
        print("    Atau jalankan dulu: python freebuds_anc.py scan")
        sys.exit(1)

    if cmd == "status":
        get_anc_status(DEVICE_MAC)
    elif cmd in ("off", "mati"):
        set_anc(DEVICE_MAC, "off")
    elif cmd in ("transparency", "awareness", "transparan"):
        set_anc(DEVICE_MAC, "awareness")
    elif cmd in ("anc", "cancelling", "noise_cancelling"):
        level = args[1].lower() if len(args) > 1 else None
        set_anc(DEVICE_MAC, "anc", level)
    else:
        print(f"[!] Perintah tidak dikenal: '{cmd}'")
        print_help()
        sys.exit(1)

