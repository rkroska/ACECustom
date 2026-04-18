Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# Config
$ServerDir  = "C:\ACE\ACEBuild"
$ServerExe  = "dotnet"
$ServerArgs = "ACE.Server.dll"
$AppTitle   = "ACE Server Manager"

# State
$script:ServerProcess = $null
$script:StartTime     = $null
$script:OutputQueue   = [System.Collections.Concurrent.ConcurrentQueue[string]]::new()
$script:StdInWriter    = $null
$script:ReaderRunspace = $null
$script:ReaderPs       = $null
$script:ReaderJob      = $null
$script:Stopping       = $false
$script:StopTime       = $null
$script:StopGraceSecs  = 8

function IsRunning {
    if ($null -eq $script:ServerProcess) { return $false }
    try { return !$script:ServerProcess.HasExited } catch { return $false }
}

function FindExistingServer {
    try {
        $procs = Get-Process -Name 'dotnet' -ErrorAction SilentlyContinue
        if ($null -eq $procs) { return $null }
        foreach ($p in $procs) {
            try {
                $wmi = Get-WmiObject Win32_Process -Filter "ProcessId=$($p.Id)" -ErrorAction SilentlyContinue
                if ($null -ne $wmi -and $wmi.CommandLine -like '*ACE.Server.dll*') { return $p }
            } catch { }
        }
    } catch { }
    return $null
}

function MakeIcon([string]$colorHex) {
    $bmp   = [System.Drawing.Bitmap]::new(32, 32)
    $g     = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $brush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml($colorHex))
    $g.FillEllipse($brush, 2, 2, 28, 28)
    $font  = [System.Drawing.Font]::new("Segoe UI", 14, [System.Drawing.FontStyle]::Bold)
    $sb    = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
    $sf    = [System.Drawing.StringFormat]::new()
    $sf.Alignment     = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $g.DrawString("A", $font, $sb, [System.Drawing.RectangleF]::new(0,0,32,32), $sf)
    $g.Dispose(); $brush.Dispose(); $font.Dispose(); $sb.Dispose(); $sf.Dispose()
    $icon = [System.Drawing.Icon]::FromHandle($bmp.GetHicon())
    $bmp.Dispose()
    return $icon
}

# -- Form -------------------------------------------------------------------------
$form = [System.Windows.Forms.Form]::new()
$form.Text            = $AppTitle
$form.Size            = [System.Drawing.Size]::new(700, 620)
$form.StartPosition   = [System.Windows.Forms.FormStartPosition]::CenterScreen
$form.BackColor       = [System.Drawing.ColorTranslator]::FromHtml("#0f1117")
$form.ForeColor       = [System.Drawing.Color]::White
$form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::Sizable
$form.MaximizeBox     = $true
$form.Icon            = MakeIcon "#4f46e5"

$lblTitle           = [System.Windows.Forms.Label]::new()
$lblTitle.Text      = "ACE Server Manager"
$lblTitle.Font      = [System.Drawing.Font]::new("Segoe UI", 18, [System.Drawing.FontStyle]::Bold)
$lblTitle.ForeColor = [System.Drawing.ColorTranslator]::FromHtml("#a5b4fc")
$lblTitle.AutoSize  = $true
$lblTitle.Location  = [System.Drawing.Point]::new(20, 15)

# Status bar
$pnlStatus           = [System.Windows.Forms.Panel]::new()
$pnlStatus.Size      = [System.Drawing.Size]::new(660, 40)
$pnlStatus.Location  = [System.Drawing.Point]::new(20, 58)
$pnlStatus.BackColor = [System.Drawing.ColorTranslator]::FromHtml("#1e2130")

$lblDot           = [System.Windows.Forms.Label]::new()
$lblDot.AutoSize  = $false
$lblDot.Size      = [System.Drawing.Size]::new(16, 16)
$lblDot.Location  = [System.Drawing.Point]::new(12, 12)
$lblDot.BackColor = [System.Drawing.ColorTranslator]::FromHtml("#ef4444")

$lblStatus           = [System.Windows.Forms.Label]::new()
$lblStatus.Text      = "  Server is OFFLINE"
$lblStatus.Font      = [System.Drawing.Font]::new("Segoe UI", 11, [System.Drawing.FontStyle]::Bold)
$lblStatus.ForeColor = [System.Drawing.Color]::White
$lblStatus.AutoSize  = $true
$lblStatus.Location  = [System.Drawing.Point]::new(34, 10)

$lblUptimePanel           = [System.Windows.Forms.Label]::new()
$lblUptimePanel.Text      = ""
$lblUptimePanel.Font      = [System.Drawing.Font]::new("Segoe UI", 9)
$lblUptimePanel.ForeColor = [System.Drawing.ColorTranslator]::FromHtml("#64748b")
$lblUptimePanel.AutoSize  = $true
$lblUptimePanel.Location  = [System.Drawing.Point]::new(430, 12)
$pnlStatus.Controls.AddRange(@($lblDot, $lblStatus, $lblUptimePanel))

# Server control buttons
function MakeBtn([string]$txt, [int]$x, [int]$w, [string]$bg) {
    $b = [System.Windows.Forms.Button]::new()
    $b.Text      = $txt
    $b.Font      = [System.Drawing.Font]::new("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)
    $b.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
    $b.FlatAppearance.BorderSize = 0
    $b.BackColor = [System.Drawing.ColorTranslator]::FromHtml($bg)
    $b.ForeColor = [System.Drawing.Color]::White
    $b.Size      = [System.Drawing.Size]::new($w, 42)
    $b.Location  = [System.Drawing.Point]::new($x, 112)
    $b.Cursor    = [System.Windows.Forms.Cursors]::Hand
    return $b
}
$btnStart   = MakeBtn "Start Server"  20  195 "#22c55e"
$btnStop    = MakeBtn "Stop Server"  228  195 "#ef4444"
$btnRestart = MakeBtn "Restart"      436  135 "#f59e0b"
$btnStop.Enabled    = $false
$btnRestart.Enabled = $false

# -- Log section header (row 1)
$lblLogHdr           = [System.Windows.Forms.Label]::new()
$lblLogHdr.Text      = "CONSOLE OUTPUT"
$lblLogHdr.Font      = [System.Drawing.Font]::new("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)
$lblLogHdr.ForeColor = [System.Drawing.ColorTranslator]::FromHtml("#6366f1")
$lblLogHdr.AutoSize  = $true
$lblLogHdr.Location  = [System.Drawing.Point]::new(20, 168)

# -- Filter row (row 2)
$lblFilter           = [System.Windows.Forms.Label]::new()
$lblFilter.Text      = "Filter:"
$lblFilter.Font      = [System.Drawing.Font]::new("Segoe UI", 9)
$lblFilter.ForeColor = [System.Drawing.ColorTranslator]::FromHtml("#64748b")
$lblFilter.AutoSize  = $true
$lblFilter.Location  = [System.Drawing.Point]::new(20, 196)

$txtFilter           = [System.Windows.Forms.TextBox]::new()
$txtFilter.Font      = [System.Drawing.Font]::new("Consolas", 9)
$txtFilter.BackColor = [System.Drawing.ColorTranslator]::FromHtml("#1e2130")
$txtFilter.ForeColor = [System.Drawing.ColorTranslator]::FromHtml("#e2e8f0")
$txtFilter.BorderStyle = [System.Windows.Forms.BorderStyle]::FixedSingle
$txtFilter.Size      = [System.Drawing.Size]::new(220, 24)
$txtFilter.Location  = [System.Drawing.Point]::new(58, 192)

$cboLevel            = [System.Windows.Forms.ComboBox]::new()
$cboLevel.Font       = [System.Drawing.Font]::new("Segoe UI", 9)
$cboLevel.BackColor  = [System.Drawing.ColorTranslator]::FromHtml("#1e2130")
$cboLevel.ForeColor  = [System.Drawing.ColorTranslator]::FromHtml("#e2e8f0")
$cboLevel.FlatStyle  = [System.Windows.Forms.FlatStyle]::Flat
$cboLevel.DropDownStyle = [System.Windows.Forms.ComboBoxStyle]::DropDownList
$cboLevel.Size       = [System.Drawing.Size]::new(130, 24)
$cboLevel.Location   = [System.Drawing.Point]::new(288, 192)
$cboLevel.Items.AddRange(@("All Lines", "WARN + ERROR", "ERROR Only"))
$cboLevel.SelectedIndex = 0

$btnPause            = [System.Windows.Forms.Button]::new()
$btnPause.Text       = "Pause"
$btnPause.Font       = [System.Drawing.Font]::new("Segoe UI", 8, [System.Drawing.FontStyle]::Bold)
$btnPause.FlatStyle  = [System.Windows.Forms.FlatStyle]::Flat
$btnPause.FlatAppearance.BorderColor = [System.Drawing.ColorTranslator]::FromHtml("#334155")
$btnPause.BackColor  = [System.Drawing.ColorTranslator]::FromHtml("#1e2130")
$btnPause.ForeColor  = [System.Drawing.ColorTranslator]::FromHtml("#fbbf24")
$btnPause.Size       = [System.Drawing.Size]::new(56, 24)
$btnPause.Location   = [System.Drawing.Point]::new(428, 192)
$btnPause.Cursor     = [System.Windows.Forms.Cursors]::Hand

$btnClear            = [System.Windows.Forms.Button]::new()
$btnClear.Text       = "Clear"
$btnClear.Font       = [System.Drawing.Font]::new("Segoe UI", 8)
$btnClear.FlatStyle  = [System.Windows.Forms.FlatStyle]::Flat
$btnClear.FlatAppearance.BorderColor = [System.Drawing.ColorTranslator]::FromHtml("#334155")
$btnClear.BackColor  = [System.Drawing.ColorTranslator]::FromHtml("#1e2130")
$btnClear.ForeColor  = [System.Drawing.ColorTranslator]::FromHtml("#94a3b8")
$btnClear.Size       = [System.Drawing.Size]::new(52, 24)
$btnClear.Location   = [System.Drawing.Point]::new(490, 192)
$btnClear.Cursor     = [System.Windows.Forms.Cursors]::Hand

# -- Log box
$txtLog             = [System.Windows.Forms.RichTextBox]::new()
$txtLog.Font        = [System.Drawing.Font]::new("Consolas", 9)
$txtLog.BackColor   = [System.Drawing.ColorTranslator]::FromHtml("#090d13")
$txtLog.ForeColor   = [System.Drawing.ColorTranslator]::FromHtml("#94a3b8")
$txtLog.ReadOnly    = $true
$txtLog.BorderStyle = [System.Windows.Forms.BorderStyle]::None
$txtLog.Size        = [System.Drawing.Size]::new(660, 318)
$txtLog.Location    = [System.Drawing.Point]::new(20, 222)
$txtLog.ScrollBars  = [System.Windows.Forms.RichTextBoxScrollBars]::Vertical
$txtLog.Anchor      = [System.Windows.Forms.AnchorStyles]::Top -bor [System.Windows.Forms.AnchorStyles]::Bottom -bor [System.Windows.Forms.AnchorStyles]::Left -bor [System.Windows.Forms.AnchorStyles]::Right

# -- Command input row (docked to bottom)
$lblCmd           = [System.Windows.Forms.Label]::new()
$lblCmd.Text      = "Command:"
$lblCmd.Font      = [System.Drawing.Font]::new("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)
$lblCmd.ForeColor = [System.Drawing.ColorTranslator]::FromHtml("#6366f1")
$lblCmd.AutoSize  = $true
$lblCmd.Location  = [System.Drawing.Point]::new(20, 554)
$lblCmd.Anchor    = [System.Windows.Forms.AnchorStyles]::Bottom -bor [System.Windows.Forms.AnchorStyles]::Left

$txtCmd           = [System.Windows.Forms.TextBox]::new()
$txtCmd.Font      = [System.Drawing.Font]::new("Consolas", 10)
$txtCmd.BackColor = [System.Drawing.ColorTranslator]::FromHtml("#1e2130")
$txtCmd.ForeColor = [System.Drawing.ColorTranslator]::FromHtml("#e2e8f0")
$txtCmd.BorderStyle = [System.Windows.Forms.BorderStyle]::FixedSingle
$txtCmd.Size      = [System.Drawing.Size]::new(528, 28)
$txtCmd.Location  = [System.Drawing.Point]::new(88, 550)
$txtCmd.Enabled   = $false
$txtCmd.Anchor    = [System.Windows.Forms.AnchorStyles]::Bottom -bor [System.Windows.Forms.AnchorStyles]::Left -bor [System.Windows.Forms.AnchorStyles]::Right

$btnSend           = [System.Windows.Forms.Button]::new()
$btnSend.Text      = "Send"
$btnSend.Font      = [System.Drawing.Font]::new("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)
$btnSend.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
$btnSend.FlatAppearance.BorderSize = 0
$btnSend.BackColor = [System.Drawing.ColorTranslator]::FromHtml("#4f46e5")
$btnSend.ForeColor = [System.Drawing.Color]::White
$btnSend.Size      = [System.Drawing.Size]::new(62, 28)
$btnSend.Location  = [System.Drawing.Point]::new(622, 550)
$btnSend.Cursor    = [System.Windows.Forms.Cursors]::Hand
$btnSend.Enabled   = $false
$btnSend.Anchor    = [System.Windows.Forms.AnchorStyles]::Bottom -bor [System.Windows.Forms.AnchorStyles]::Right

$form.Controls.AddRange(@(
    $lblTitle, $pnlStatus,
    $btnStart, $btnStop, $btnRestart,
    $lblLogHdr, $lblFilter, $txtFilter, $cboLevel, $btnPause, $btnClear,
    $txtLog,
    $lblCmd, $txtCmd, $btnSend
))

# Filter/pause state
$script:LogPaused = $false
$script:MaxLines  = 400

# -- Tray -------------------------------------------------------------------------
$tray                  = [System.Windows.Forms.NotifyIcon]::new()
$tray.Text             = $AppTitle
$tray.Icon             = MakeIcon "#4f46e5"
$tray.Visible          = $true
$ctx                   = [System.Windows.Forms.ContextMenuStrip]::new()
$miShow                = $ctx.Items.Add("Show Manager")
$miTrayStart           = $ctx.Items.Add("Start Server")
$miTrayStop            = $ctx.Items.Add("Stop Server")
$ctx.Items.Add("-")    | Out-Null
$miExit                = $ctx.Items.Add("Exit")
$tray.ContextMenuStrip = $ctx

# -- Log helpers -------------------------------------------------------------------
function ShouldShowLine([string]$line) {
    $level = $cboLevel.SelectedIndex
    if ($level -eq 1 -and $line -notmatch 'WARN|Warning|ERROR|Exception|FATAL') { return $false }
    if ($level -eq 2 -and $line -notmatch 'ERROR|Exception|FATAL') { return $false }
    $f = $txtFilter.Text.Trim()
    if ($f -ne '' -and $line -notmatch [regex]::Escape($f)) { return $false }
    return $true
}

function WriteLog([string]$line) {
    if ($script:LogPaused) { return }
    if (!(ShouldShowLine $line)) { return }
    if ($txtLog.Lines.Count -gt $script:MaxLines) {
        $keep = [int]($script:MaxLines * 0.75)
        $saved = $txtLog.Lines | Select-Object -Last $keep
        $txtLog.SuspendLayout()
        $txtLog.Clear()
        foreach ($l in $saved) { $txtLog.AppendText("$l`n") }
        $txtLog.ResumeLayout()
    }
    $txtLog.SelectionStart  = $txtLog.TextLength
    $txtLog.SelectionLength = 0
    if ($line -match 'ERROR|Exception|FATAL') {
        $txtLog.SelectionColor = [System.Drawing.ColorTranslator]::FromHtml("#fca5a5")
    } elseif ($line -match 'WARN|Warning') {
        $txtLog.SelectionColor = [System.Drawing.ColorTranslator]::FromHtml("#fbbf24")
    } elseif ($line -match 'started|online|ready|listen|Startup') {
        $txtLog.SelectionColor = [System.Drawing.ColorTranslator]::FromHtml("#86efac")
    } else {
        $txtLog.SelectionColor = [System.Drawing.ColorTranslator]::FromHtml("#94a3b8")
    }
    $txtLog.AppendText("$line`n")
    $txtLog.ScrollToCaret()
}

$btnPause.Add_Click({
    $script:LogPaused = !$script:LogPaused
    if ($script:LogPaused) {
        $btnPause.Text      = "Resume"
        $btnPause.ForeColor = [System.Drawing.ColorTranslator]::FromHtml("#22c55e")
    } else {
        $btnPause.Text      = "Pause"
        $btnPause.ForeColor = [System.Drawing.ColorTranslator]::FromHtml("#fbbf24")
    }
})
$btnClear.Add_Click({ $txtLog.Clear() })

# -- UI state ----------------------------------------------------------------------
function RefreshButtons {
    $running = IsRunning
    if ($script:Stopping) {
        # Mid-shutdown grace period
        $lblDot.BackColor    = [System.Drawing.ColorTranslator]::FromHtml("#f59e0b")
        $lblStatus.Text      = "  Server is STOPPING..."
        $btnStart.Enabled    = $false
        $btnStop.Enabled     = $false
        $btnRestart.Enabled  = $false
        $miTrayStart.Enabled = $false
        $miTrayStop.Enabled  = $false
        $txtCmd.Enabled      = $false
        $btnSend.Enabled     = $false
        return
    }
    if ($running) {
        $lblDot.BackColor    = [System.Drawing.ColorTranslator]::FromHtml("#22c55e")
        $lblStatus.Text      = "  Server is ONLINE"
        $btnStart.Enabled    = $false
        $btnStop.Enabled     = $true
        $btnRestart.Enabled  = $true
        $miTrayStart.Enabled = $false
        $miTrayStop.Enabled  = $true
        $tray.Icon           = MakeIcon "#22c55e"
        $txtCmd.Enabled      = ($null -ne $script:StdInWriter)
        $btnSend.Enabled     = ($null -ne $script:StdInWriter)
    } else {
        $lblDot.BackColor    = [System.Drawing.ColorTranslator]::FromHtml("#ef4444")
        $lblStatus.Text      = "  Server is OFFLINE"
        $lblUptimePanel.Text = ""
        $btnStart.Enabled    = $true
        $btnStop.Enabled     = $false
        $btnRestart.Enabled  = $false
        $miTrayStart.Enabled = $true
        $miTrayStop.Enabled  = $false
        $tray.Icon           = MakeIcon "#4f46e5"
        $txtCmd.Enabled      = $false
        $btnSend.Enabled     = $false
    }
}

# -- Poll timer -------------------------------------------------------------------
$pollTimer          = [System.Windows.Forms.Timer]::new()
$pollTimer.Interval = 200

$pollTimer.Add_Tick({
    # -- Grace-period stop logic -----------------------------------------------
    if ($script:Stopping -and $null -ne $script:ServerProcess) {
        $exited = $false
        try { $exited = $script:ServerProcess.HasExited } catch { $exited = $true }
        $elapsed = (Get-Date) - $script:StopTime

        if ($exited) {
            $script:Stopping      = $false
            $script:ServerProcess = $null
            $script:StdInWriter   = $null
            $lblUptimePanel.Text  = ""
            StopReader
            RefreshButtons
            WriteLog "[$(Get-Date -f 'HH:mm:ss')] Server stopped gracefully."
        } elseif ($elapsed.TotalSeconds -gt $script:StopGraceSecs) {
            WriteLog "[$(Get-Date -f 'HH:mm:ss')] Grace period expired - force stopping..."
            $script:Stopping = $false
            try { $script:ServerProcess.Kill($true) } catch { try { $script:ServerProcess.Kill() } catch { } }
            $script:ServerProcess = $null
            $script:StdInWriter   = $null
            $lblUptimePanel.Text  = ""
            StopReader
            RefreshButtons
            WriteLog "[$(Get-Date -f 'HH:mm:ss')] Server force stopped."
        }
        return  # Skip normal poll while stopping
    }

    # Drain stdout queue from background reader
    $line = $null
    $batch = 0
    while ($script:OutputQueue.TryDequeue([ref]$line) -and $batch -lt 50) {
        WriteLog $line
        $batch++
    }

    # Uptime
    if (IsRunning -and $null -ne $script:StartTime) {
        $e = (Get-Date) - $script:StartTime
        $lblUptimePanel.Text = "Uptime: {0:D2}:{1:D2}:{2:D2}" -f [int]$e.TotalHours, $e.Minutes, $e.Seconds
    }

    # Detect unexpected exit
    if ($null -ne $script:ServerProcess) {
        try {
            if ($script:ServerProcess.HasExited) {
                WriteLog "[$(Get-Date -f 'HH:mm:ss')] Server process exited (code $($script:ServerProcess.ExitCode))."
                $script:ServerProcess    = $null
                $script:StdInWriter      = $null
                $lblUptimePanel.Text     = ""
                StopReader   # Already exited - ReadLine returned null, safe to dispose
                RefreshButtons
            }
        } catch { }
    }
})

# -- Stdout reader runspace --------------------------------------------------------
function StartReader($proc, $queue) {
    StopReader
    $rs = [System.Management.Automation.Runspaces.RunspaceFactory]::CreateRunspace()
    $rs.ApartmentState = [System.Threading.ApartmentState]::STA
    $rs.ThreadOptions   = [System.Management.Automation.Runspaces.PSThreadOptions]::ReuseThread
    $rs.Open()
    $rs.SessionStateProxy.SetVariable("proc",  $proc)
    $rs.SessionStateProxy.SetVariable("queue", $queue)
    $ps = [System.Management.Automation.PowerShell]::Create()
    $ps.Runspace = $rs
    [void]$ps.AddScript({
        try {
            while (!$proc.HasExited) {
                $line = $proc.StandardOutput.ReadLine()
                if ($null -ne $line) { $queue.Enqueue($line) }
            }
            # Drain any remaining output
            $remainder = $proc.StandardOutput.ReadToEnd()
            if ($remainder -ne '') {
                foreach ($l in $remainder -split "`n") { $queue.Enqueue($l) }
            }
        } catch { }
    })
    # Store $ps persistently - local variables go out of scope and can be GC'd,
    # which would silently kill the background read loop.
    $script:ReaderPs       = $ps
    $script:ReaderJob      = $ps.BeginInvoke()
    $script:ReaderRunspace = $rs
}

function StopReader {
    try {
        if ($null -ne $script:ReaderPs) {
            $script:ReaderPs.Dispose()
        }
    } catch { }
    try {
        if ($null -ne $script:ReaderRunspace) {
            # Dispose() without Close() - avoids blocking on a stuck ReadLine().
            # Kill() must be called before this so the background thread unblocks.
            $script:ReaderRunspace.Dispose()
        }
    } catch { }
    $script:ReaderPs       = $null
    $script:ReaderRunspace = $null
    $script:ReaderJob      = $null
}

# -- Start / Stop ------------------------------------------------------------------
function StartServer {
    if (IsRunning) { return }
    WriteLog "[$(Get-Date -f 'HH:mm:ss')] Starting ACE server..."
    try {
        $psi = [System.Diagnostics.ProcessStartInfo]::new()
        $psi.FileName               = $ServerExe
        $psi.Arguments              = $ServerArgs
        $psi.WorkingDirectory       = $ServerDir
        $psi.UseShellExecute        = $false
        $psi.RedirectStandardInput  = $true
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError  = $false   # let stderr go to the void; avoids deadlock
        $psi.CreateNoWindow         = $true

        $script:ServerProcess = [System.Diagnostics.Process]::Start($psi)
        $script:StdInWriter   = $script:ServerProcess.StandardInput
        $script:StartTime     = Get-Date

        # Start background reader - ONLY pushes to queue, never touches UI
        StartReader $script:ServerProcess $script:OutputQueue

        RefreshButtons
        WriteLog "[$(Get-Date -f 'HH:mm:ss')] Server started (PID $($script:ServerProcess.Id)). Commands enabled."
    } catch {
        WriteLog "[ERROR] $_"
    }
}

function StopServer {
    if (!(IsRunning) -or $script:Stopping) { return }

    if ($null -ne $script:StdInWriter) {
        # Graceful path: send stop-now, then let the timer handle cleanup
        WriteLog "[$(Get-Date -f 'HH:mm:ss')] Sending stop-now... (force kill in $($script:StopGraceSecs)s if needed)"
        try {
            $script:StdInWriter.WriteLine("stop-now")
            $script:StdInWriter.Flush()
        } catch { }
        $script:Stopping  = $true
        $script:StopTime  = Get-Date
        RefreshButtons  # Show STOPPING... immediately
    } else {
        # No stdin (externally started server) - force kill immediately
        WriteLog "[$(Get-Date -f 'HH:mm:ss')] No stdin available - force stopping..."
        try { $script:ServerProcess.Kill($true) } catch { try { $script:ServerProcess.Kill() } catch { } }
        $script:ServerProcess = $null
        $lblUptimePanel.Text  = ""
        StopReader
        RefreshButtons
        WriteLog "[$(Get-Date -f 'HH:mm:ss')] Server force stopped."
    }
}

function SendCommand {
    $cmd = $txtCmd.Text.Trim()
    if ($cmd -eq '' -or $null -eq $script:StdInWriter) { return }
    try {
        $script:StdInWriter.WriteLine($cmd)
        $script:StdInWriter.Flush()
        WriteLog "> $cmd" "#c4b5fd"
        $txtCmd.Clear()
    } catch {
        WriteLog "[ERROR] Could not send command: $_"
    }
}

# -- Wire events -------------------------------------------------------------------
$btnStart.Add_Click({   StartServer })
$btnStop.Add_Click({    StopServer  })
$btnRestart.Add_Click({ StopServer; Start-Sleep -Milliseconds 1500; StartServer })

# Send on button click or Enter key
$btnSend.Add_Click({ SendCommand })
$txtCmd.Add_KeyDown({
    param($s, $e)
    if ($e.KeyCode -eq [System.Windows.Forms.Keys]::Enter) {
        SendCommand
        $e.SuppressKeyPress = $true
    }
})

$miShow.Add_Click({
    $form.Show()
    $form.WindowState = [System.Windows.Forms.FormWindowState]::Normal
    $form.Activate()
})
$miTrayStart.Add_Click({ StartServer })
$miTrayStop.Add_Click({  StopServer  })
$miExit.Add_Click({
    if (IsRunning) { StopServer }
    $pollTimer.Stop()
    StopReader
    $tray.Visible = $false
    $tray.Dispose()
    [System.Windows.Forms.Application]::Exit()
})

$tray.Add_DoubleClick({
    $form.Show()
    $form.WindowState = [System.Windows.Forms.FormWindowState]::Normal
    $form.Activate()
})

$form.Add_FormClosing({
    param($s,$e)
    # Stop server and fully exit on X
    if (IsRunning) { StopServer }
    $pollTimer.Stop()
    StopReader
    $tray.Visible = $false
    $tray.Dispose()
    # Allow the close to proceed normally
})

# -- Startup -----------------------------------------------------------------------
$existing = FindExistingServer
if ($null -ne $existing) {
    $script:ServerProcess = $existing
    $script:StartTime     = Get-Date
    RefreshButtons
    WriteLog "[$(Get-Date -f 'HH:mm:ss')] Detected existing ACE server (PID $($existing.Id))."
    WriteLog "[Note] Stop and Start via this manager to enable command input."
} else {
    WriteLog "ACE Server Manager ready. Click Start Server to begin."
}

$pollTimer.Start()
[System.Windows.Forms.Application]::Run($form)
