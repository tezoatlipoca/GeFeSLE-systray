# GeFeSLE-systray
Generic, Federated, Subscribable List Engine - SysTray/Dock applet

## Installation

GeFeSLE-systray provides multiple installation options for different platforms. Choose the method that best suits your system:

### Windows

#### Option 1: Windows Installer (Recommended)
Download and run the Windows installer:

```bash
# Using curl (Windows 10/11 with curl built-in)
curl -L -o GeFeSLE-systray-setup.exe "https://github.com/tezoatlipoca/GeFeSLE-systray/releases/download/v0.1.0/GeFeSLE-systray_0.1.0_win-x64.setup.exe"

# Then run the installer
GeFeSLE-systray-setup.exe
```

#### Option 2: Manual Installation (Portable)
Download the portable ZIP archive:

```bash
# Using curl
curl -L -o GeFeSLE-systray-win64.zip "https://github.com/tezoatlipoca/GeFeSLE-systray/releases/download/v0.1.0/GeFeSLE-systray_0.1.0_win-x64.zip"

# Extract and run
# Extract the ZIP file to your preferred location
# Run GeFeSLE-systray.exe
```

### Linux

#### Option 1: Debian/Ubuntu (.deb package)
For Debian, Ubuntu, and other Debian-based distributions:

```bash
# Download the .deb package
curl -L -o gefesle-systray.deb "https://github.com/tezoatlipoca/GeFeSLE-systray/releases/download/v0.1.0/GeFeSLE-systray_0.1.0_linux_x64.deb"

# Install using dpkg
sudo dpkg -i gefesle-systray.deb

# If there are dependency issues, fix them with:
sudo apt-get install -f
```

#### Option 2: Red Hat/CentOS/Fedora (.rpm package)
For Red Hat, CentOS, Fedora, and other RPM-based distributions:

```bash
# Download the .rpm package
curl -L -o gefesle-systray.rpm "https://github.com/tezoatlipoca/GeFeSLE-systray/releases/download/v0.1.0/GeFeSLE-systray_0.1.0_linux_x64.rpm"

# Install using rpm (CentOS/RHEL)
sudo rpm -i gefesle-systray.rpm

# Or using dnf (Fedora)
sudo dnf install gefesle-systray.rpm

# Or using yum (older systems)
sudo yum install gefesle-systray.rpm
```

#### Option 3: Manual Installation (Any Linux Distribution)
Download the tar.gz archive for manual installation:

```bash
# Download the archive
curl -L -o gefesle-systray-linux.tar.gz "https://github.com/tezoatlipoca/GeFeSLE-systray/releases/download/v0.1.0/GeFeSLE-systray_0.1.0_linux_x64.tar.gz"

# Extract to a local directory
tar -xzf gefesle-systray-linux.tar.gz

# Move to a directory in your PATH (optional)
sudo mv GeFeSLE-systray /usr/local/bin/

# Make executable (if needed)
chmod +x /usr/local/bin/GeFeSLE-systray

# Run the application
GeFeSLE-systray
```

### Verification

After installation, you can verify the installation by checking the version:

```bash
# Linux (if installed to PATH)
GeFeSLE-systray --version

# Windows (from installation directory)
GeFeSLE-systray.exe --version
```

### System Requirements

- **Windows**: Windows 10 or later (x64)
- **Linux**: x64 Linux distribution with glibc 2.27 or later
- **Memory**: Minimum 512 MB RAM
- **Disk**: ~50 MB available space

### Updating

To update GeFeSLE-systray, simply download and install the latest version using the same method you used for the initial installation. The new version will replace the old one.

### Uninstallation

- **Windows Installer**: Use "Add or Remove Programs" in Windows Settings
- **Windows Manual**: Delete the extracted folder
- **Linux .deb**: `sudo apt-get remove gefesle-systray`
- **Linux .rpm**: `sudo rpm -e GeFeSLE-systray` or `sudo dnf remove GeFeSLE-systray`
- **Linux Manual**: Delete the binary from `/usr/local/bin/GeFeSLE-systray`
