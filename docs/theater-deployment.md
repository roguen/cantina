# Running the theater

How Barkeep is installed on the theater PC, how it comes back after a reboot, and how a
new build gets there. The reasoning is D-037.

## The pieces

| Path | What it is |
|---|---|
| `C:\ProgramData\Cantina\app\` | The published Barkeep. Never run Barkeep from the repository's build tree: a running copy there locks files and later builds fail silently. |
| `C:\ProgramData\Cantina\theater.json` | Every theater setting in one file: LAN binding, certificate paths, acquisition folder, pairing email. [`tools/theater/theater.example.json`](../tools/theater/theater.example.json) shows the shape. It holds no secrets; the SMTP password is a separate file it points at. |
| `C:\ProgramData\Cantina\secrets\` | The SMTP password, ACL-restricted. Never in `theater.json`, never on a command line. |
| `C:\ProgramData\Cantina\certs\` | The Let's Encrypt certificate, delivered by the NAS's acme.sh deploy hop (D-029). Barkeep reloads it when it changes. |
| `C:\ProgramData\Cantina\logs\` | One log per start, the newest ten kept. Pairing codes and certificate warnings appear here. |
| `C:\ProgramData\Cantina\tools\Start-Barkeep.ps1` | The launcher the startup task runs, copied out of the repository so branch switches can never break the boot path. |

## Startup and the watchdog

`tools/theater/Install-TheaterTask.ps1` registers one scheduled task, **Cantina Barkeep**,
for the signed-in operator, with no elevation. It has two triggers:

- **At sign-in**: the boot path.
- **Every five minutes**: the watchdog.

Both run `Start-Barkeep.ps1`, which does nothing when Barkeep is already running, so a crash
costs at most five minutes and a healthy theater is never touched.

```powershell
.\tools\theater\Install-TheaterTask.ps1           # register or update
.\tools\theater\Install-TheaterTask.ps1 -Remove   # unregister
```

**After a reboot the theater comes up when the operator's account signs in, not before.**
That is deliberate. Barkeep sends keystrokes to YARG, and a Windows service, or a task set to
"run whether the user is logged on or not", runs in a session whose input reaches no desktop
(D-024). If the PC should come back unattended, sign-in must happen unattended too. That is
a Windows setting (automatic sign-in) and the operator's call; Cantina does not change it.

YARG is started by Barkeep, not by the task (D-038). With
`YargProcess:LaunchAtStartup` on, Barkeep launches YARG a few seconds after it starts, waits
until the library has loaded, and opens the Music Library, so a reboot followed by the
operator's sign-in brings the whole theater back. The iPad's Stage tab shows YARG's state and
offers **Start YARG** when it is not running and **Restart YARG** (two taps) when controllers
stop responding.

## Deploying a build

From a checkout of the commit to deploy:

```powershell
.\tools\theater\Deploy-Theater.ps1
```

It builds the client, publishes the server to a staging folder, and only then stops the
running Barkeep, copies the new build in, starts it through the launcher, and waits for
`https://cantina.aero4ge.com/` to answer 200. A failed build never takes the theater down.

## The firewall

The operator's inbound allow rule, **Cantina Barkeep** (TCP 80 and 443, LAN subnet only), is
scoped to `C:\ProgramData\Cantina\app\Cantina.Barkeep.exe`. The published path must not move.
If Windows Defender ever prompts for Barkeep, **declining writes Block rules named
`Cantina.Barkeep`** that override the allow rule; removing those is an operator task
(`netsh advfirewall firewall delete rule name="Cantina.Barkeep"` in an elevated prompt).
