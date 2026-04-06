# ACE Server - Operations & Recovery Guide

This document provides a technical guide for the maintenance, recovery, and operation of the ACE Server and its integrated Web Portal.

## 1. High-Level Architecture Overview

### Core Components
- **ACE Server (`dotnet`)**: The main game server process.
- **MySQL / MariaDB**: Authority for the World and Character databases.
- **`systemd` Watchdog**: A system-level service that ensures the server process remains active.
- **`screen` Session**: Provides interactive console access for the live server.
- **Service Account**: `acerunner` (locked account with restricted permissions).

---

## 2. Access & Permissions

### Service Account: `acerunner`
- Owns the ACE process and all `/ace` runtime directories.
- Password is locked; access is granted via `sudo` from authorized admin accounts.

### Admin Access
Authorized administrators can assume the service account identity to perform maintenance:
```bash
sudo -iu acerunner
```

### Sudoers Configuration
Admin rules are typically defined in `/etc/sudoers.d/acerunner-access`:
```text
<admin_username> ALL=(acerunner) NOPASSWD: ALL
```
> [!CAUTION]
> Anyone with this sudo privilege has full control over the game server runtime.

---

## 3. Directory Layout

/ace
 ├── Server/            # ACE source repository
 ├── serverbuild/       # Current binary & asset distribution (published output)
 ├── Dats/              # Client DAT files (Portal, Cell, etc.)
 ├── config/            # Externalized configuration storage (Config.js)
 ├── logs/              # Runtime diagnostics (owned by acerunner)
 ├── crashdumps/        # Core dumps and stack traces for diagnostics
 ├── backups/
 │   └── db/            # Compressed SQL snapshots
```

---

## 4. Normal Operations

### Attaching to the Console
To view live server logs or issue commands:
1.  Assume the service identity: `sudo -iu acerunner`
2.  Attach to the session: `screen -r ace`
3.  **To Detach Safely**: Press `Ctrl+A` then `D`. **Do not use Ctrl+C** or you will kill the server.

### Process Verification
Check if the server is active:
```bash
screen -ls
pgrep -af ACE.Server.dll
```

---

## 5. Maintenance Mode

Maintenance mode prevents the `systemd` watchdog from automatically restarting the server, allowing for safe patching or manual troubleshooting.

### Enabling Maintenance
```bash
sudo mkdir -p /run/ace
sudo touch /run/ace/maintenance
```

### Disabling Maintenance
```bash
sudo rm -f /run/ace/maintenance
```

---

## 6. Patching & Build Procedures

### Server Update Lifecycle

Follow these steps to perform a safe, version-controlled update and build of the ACE Server:

1.  **Pull Latest Source**: Fetch the newest code from the repository while the server is still running.
    ```bash
    cd /ace/Server && git pull
    ```

2.  **Build & Prepare Staging Area**: Build the project into a separate staging directory. This avoids "file in use" errors and builds the Server & Web Portal SPA in the background while the server is still running.
    ```bash
    sudo -iu acerunner bash -lc 'source /etc/profile.d/dotnet.sh && cd /ace/Server/Source && dotnet publish -c Release -o /ace/serverbuildstaging'
    ```

3.  **Symlink Authority Configuration**: Ensure the staging directory correctly points to the master configuration.
    ```bash
    sudo -iu acerunner bash -lc 'rm -f /ace/serverbuildstaging/Config.js && ln -s /ace/config/Config.js /ace/serverbuildstaging/Config.js'
    ```

4.  **Enable Maintenance Mode**:
    ```bash
    sudo mkdir -p /run/ace && sudo touch /run/ace/maintenance
    ```

5.  **Perform Database Backup**:
    ```bash
    sudo /usr/local/bin/backup-mysql-all.sh
    ```

6.  **Atomic Binary Swap**: Move the current live directory to a backup location and swap in the new staging build.
    ```bash
    # Clean up previous rollback target and move current live to backup
    sudo rm -rf /ace/serverbuildold && [ -d /ace/serverbuild ] && sudo mv /ace/serverbuild /ace/serverbuildold

    # Move staged build to production
    sudo mv /ace/serverbuildstaging /ace/serverbuild
    ```

7.  **Restart via Watchdog**: Launch the newly deployed server version.
    ```bash
    sudo systemctl restart ace-watchdog
    ```

8.  **Monitor Verification**:
    ```bash
    pgrep -af "dotnet .*ACE\.Server\.dll"
    tail -n 50 /ace/logs/ace.console.log
    ```

9.  **Disable Maintenance Mode**:
    ```bash
    sudo rm -f /run/ace/maintenance
    ```

---

## 7. Web Portal Operations

The Web Portal provides administrative tools and account management.

### Automated Build Support
The `ACE.Server.csproj` is configured to **automatically build** the Web Portal SPA whenever a `publish` command is issued. 
- Running `dotnet publish` will trigger `npm install` and `npm run build` in the `ClientApp` directory.
- The resulting assets are bundled into a `wwwroot/` folder adjacent to the server binary.

### Deployment & Serving Strategy
The `ACE.Server` uses an **Environment-Aware** strategy to serve the portal:
- **Development Mode**: Priorities the source tree `dist` folder (`Source/ACE.WebPortal/ClientApp/dist`) on port 5000.
- **Production Mode**: Resolves to the local `wwwroot/` folder and binds to **localhost:5001** only.

> [!IMPORTANT]
> **Fail-Fast Guard**: The server will **refuse to start** if it cannot find the Web Portal assets. If the server fails with a FATAL log about missing assets, ensure that `wwwroot` (production) or `dist` (development) exists.

### Reverse Proxy & Buffer Hardening
If you encounter **400 Bad Request** errors (Header Too Large), you must adjust your NGINX buffers. Refer to the:
**[➜ Hardened Linux Setup Guide (readme_linux_setup.md)](./readme_linux_setup.md)**

---

## 8. Troubleshooting & Diagnostics

### Startup Checklist
1.  **Maintenance Flag**: Ensure `/run/ace/maintenance` isn't blocking the watchdog.
2.  **Watchdog Logs**: `journalctl -u ace-watchdog -n 100`
3.  **Error Logs**: Review `/ace/logs/ace.stderr.log` for unhandled exceptions.
4.  **Binary Path**: Verify `ACE.Server.dll` exists in `/ace/serverbuild/`.

### Crash Handling
If the server crashes, collect the following for review:
- The specific crash dump in `/ace/crashdumps/`.
- The tail of the `ace.stderr.log`.
- Any relevant logs from the `Watchdog` service.

---

## 9. Contacts & Recovery Philosophy

- **Server Owner**: beef, schneebly
- **Backup Admin**: ruggan
- **Support**: https://discord.com/channels/1097369623374082118/1316229984548946021

> [!IMPORTANT]
> **Philosophy**: Always prioritize **Recovery and Stability** over experimentation during live incidents. Preserve all crash data for post-mortem analysis.

---

## 10. Rollback Procedure

If a new binary deployment is unstable, corrupt, or fails to start, follow these steps to immediately restore the last known-good version.

### 1. Enable Maintenance Mode
Prevents the watchdog from fighting manual recovery.
```bash
sudo mkdir -p /run/ace && sudo touch /run/ace/maintenance
```

### 2. Swap Failed Build with Rollback Backup
This restores the binary and assets from `/ace/serverbuildold`.
```bash
# Move the failed build to a diagnostic path and restore backup
sudo mv /ace/serverbuild /ace/serverbuildfailed && sudo mv /ace/serverbuildold /ace/serverbuild
```

### 3. Restart and Monitor
```bash
sudo systemctl restart ace-watchdog
pgrep -af "dotnet .*ACE\.Server\.dll"
tail -n 50 /ace/logs/ace.console.log
```

### 4. Disable Maintenance Mode
Once the server is confirmed stable on the old version.
```bash
sudo rm -f /run/ace/maintenance
```

---
This document should be updated whenever the server architecture or build pipeline changes.
