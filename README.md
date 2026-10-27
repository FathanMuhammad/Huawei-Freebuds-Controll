# Huawei FreeBuds SE4 ANC Controller 🎧

Aplikasi Windows (.NET 8 WPF) dan script Python untuk mengontrol Active Noise Cancellation (ANC) pada TWS **Huawei FreeBuds SE4 ANC** melalui koneksi Bluetooth RFCOMM/SPP tanpa memerlukan aplikasi resmi HP.

---

## 🌟 Fitur Utama

- **3 Mode Kontrol Lengkap (Sesuai Aplikasi HP)**:
  - 🔇 **Off**: Mode standar tanpa peredam suara bising.
  - 👂 **Awareness**: Mode transparansi suara lingkungan sekitar tetap terdengar jelas.
  - 🔕 **Noise Cancelling**: Peredam bising aktif dengan 4 pilihan intensitas:
    - *General* (Standar)
    - *Cozy* (Ringan / Nyaman di ruangan sepi)
    - *Ultra* (Maksimal untuk kebisingan tinggi)
    - *Dynamic* (Otomatis menyesuaikan lingkungan)
- **Aplikasi Windows Modern (.NET 8 WPF)**:
  - Desain antarmuka Fluent Dark Mode (Windows 11 style).
  - Menggunakan native Windows Bluetooth Socket (`AF_BTH` / `SOCKADDR_BTH`), tanpa ketergantungan library luar (zero external dependencies).
  - **System Tray (Taskbar)**: Klik kanan icon tray untuk ganti mode secara instan tanpa membuka jendela utama.
  - **Auto-Detect MAC Address**: Deteksi otomatis alamat Bluetooth FreeBuds yang sudah ter-pair.
  - **Status Aktual Realtime**: Menampilkan status mode yang sedang aktif langsung dari headset.
  - **Keyboard Shortcuts**:
    - `F1` atau `1` : Mode Off
    - `F2` atau `2` : Mode Awareness
    - `F3` atau `3` : Mode Noise Cancelling
    - `F5` : Refresh status
    - `Esc` : Minimize ke System Tray
- **Script CLI & GUI Python (`freebuds_anc.py`)**:
  - Script mandiri tanpa perlu install library tambahan (`pip`).
  - Mendukung command-line interface dan antarmuka Tkinter.

---

## 🚀 Cara Menjalankan

### 1. Menggunakan Aplikasi .NET (Rekomendasi)

#### Menjalankan langsung via .NET CLI:
```bash
dotnet run --project FreeBudsController\FreeBudsController.csproj
```

#### Kompilasi executable rilis (Release):
Cukup jalankan script batch:
```bash
publish.bat
```
File `FreeBudsController.exe` siap pakai akan otomatis dibuat di folder `publish\`.

---

### 2. Menggunakan Script Python (`freebuds_anc.py`)

Pastikan FreeBuds sudah di-pair ke laptop dan Bluetooth menyala.

```bash
# Cek status mode saat ini
python freebuds_anc.py status

# Ubah ke mode Awareness
python freebuds_anc.py awareness

# Ubah ke mode Noise Cancelling
python freebuds_anc.py anc
python freebuds_anc.py anc cozy
python freebuds_anc.py anc ultra
python freebuds_anc.py anc general

# Matikan ANC (Off)
python freebuds_anc.py off

# Buka GUI Tkinter
python freebuds_anc.py gui
```

---

## 🛠️ Protokol Bluetooth Huawei (Reverse Engineered)

- **Port RFCOMM**: Default FreeBuds SE 4 ANC berada di **Port 1**.
- **Command Set ANC Mode**: `0x2B 0x04`
  - TLV Parameter 1 terdiri dari **2 bytes** `[Mode, Level]`:
    - `Off`: `0x00 0x00`
    - `Noise Cancelling`: `0x01 0xFF` *(atau `0x01 [Level]`)*
    - `Awareness`: `0x02 0xFF`
- **Command Get ANC Status**: `0x2B 0x2A`
  - Mengembalikan 2 byte status: `[Level, Mode]`.
- **Integritas Paket**: Menggunakan frame header magic `0x5A`, padding, dan checksum **CRC16-XModem**.

---

## 📜 Lisensi
Open source untuk keperluan personal dan komunitas audio.
