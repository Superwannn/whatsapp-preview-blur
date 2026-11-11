# Publikasi Preview Blur ke GitHub

Git sudah tersedia pada komputer yang digunakan untuk menyiapkan proyek ini. Langkah berikut dijalankan sendiri melalui PowerShell.

## 1. Buat repository kosong

Buka [repository baru](https://github.com/new), pilih nama `whatsapp-preview-blur`, tentukan Public atau Private, lalu klik **Create repository**.

Biarkan pilihan README, `.gitignore`, dan license kosong pada langkah ini karena proyek lokal sudah memiliki README dan `.gitignore`.

## 2. Buat commit pertama

```powershell
cd "D:\My Document\Project GPT\WhatsAppPreviewBlur"
git init -b main
git add .
git status
git commit -m "Initial release: WhatsApp Preview Blur"
```

Pada `git status`, periksa bahwa yang masuk hanyalah kode, dokumentasi, screenshot pengaturan, dan file build. File `.exe`, `settings.txt`, serta hasil pengujian sudah dikecualikan.

Jika commit meminta identitas, jalankan dua perintah berikut, ganti isinya dengan nama dan alamat email milikmu, lalu ulangi `git commit`. Pengaturan ini hanya berlaku pada repository ini.

```powershell
git config user.name "Nama kamu"
git config user.email "Email GitHub atau email noreply milikmu"
```

## 3. Hubungkan dan push

Ganti `USERNAME` dengan username GitHub milikmu. Jika nama repository berbeda, ganti juga nama repository pada URL.

```powershell
git remote add origin https://github.com/USERNAME/whatsapp-preview-blur.git
git push -u origin main
```

Selesaikan login GitHub apabila diminta oleh Git Credential Manager. Setelah push berhasil, muat ulang halaman repository.

## 4. Bagikan aplikasi siap pakai

Kode masuk ke repository; aplikasi siap pakai dibagikan melalui **Releases**:

1. Pada halaman repository, buka **Releases** lalu **Draft a new release** atau **Create a new release**.
2. Buat tag `v0.10.0` pada branch `main`.
3. Isi judul, misalnya `Preview Blur v0.10.0`.
4. Lampirkan file `PreviewBlur.exe` dari folder proyek.
5. Klik **Publish release**.

Jangan lampirkan `settings.txt`; pengguna dapat membuat preferensi mereka sendiri. Nama aplikasi utama tetap `PreviewBlur.exe` agar tidak perlu mengganti nama file pada setiap rilis.

## Pembaruan berikutnya

```powershell
git add .
git commit -m "Describe the changes"
git push
```

Bangun ulang aplikasi menggunakan `./build.ps1` sebelum membuat release baru. Keluar dari aplikasi terlebih dahulu apabila Windows melaporkan bahwa file sedang dipakai.

## Lisensi

Jika repository akan menerima penggunaan atau kontribusi publik sebagai proyek sumber terbuka, pilih lisensi yang sesuai, misalnya MIT jika itu sesuai keinginanmu. Proyek ini belum menetapkan lisensi secara otomatis.

Referensi: [panduan resmi GitHub untuk kode lokal](https://docs.github.com/en/migrations/importing-source-code/using-the-command-line-to-import-source-code/adding-locally-hosted-code-to-github), [pengelolaan release](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository).
