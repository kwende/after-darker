# WineVDM with the original After Dark host

Trial started 2026-09-21 on `codex/winevdm-original-host`. This is an isolated
whole-application compatibility experiment, not a replacement of our runtime.

## Question

Can the original After Dark host load its own modules, display its original
controls and run fullscreen through WineVDM, without our NE loader, Unicorn
instance or hand rolled Windows API implementations?

## Reproducible inputs

- WineVDM release: [v0.9.0](https://github.com/otya128/winevdm/releases/tag/v0.9.0).
- Archive: `otvdm-v0.9.0.zip`, 1,534,798 bytes, SHA-256
  `842B11AED5FA81F3E1D4272E0EE7D37F1A5A8F936DE825309DDA672835E16FD4`.
- Original NE host: `ADW30.EXE`, SHA-256
  `B3881912392A4A9324945AB7D82C17D306ED65D650FDED5638EB9FAF16DB2D18`.
- Copy the entire extracted AFTERDRK installation, including its helper DLLs,
  module subfolders and INI files. Do not alter the source collection.
- Extract WineVDM beside that working copy. The local experiment lives under
  ignored `artifacts/winevdm-original-host/`. No installer or file-association
  registration was run, and the default software CPU backend was retained.

Launch the portable `otvdm.exe` with the full path to the copied `ADW30.EXE` as
its argument and the copied AFTERDRK directory as its working directory. Capture
stdout/stderr separately. No guessed screensaver command-line switch is needed
for the first interface test.

## First observation

The original executable loads and creates an **After Dark** startup dialog.
It reports that the After Dark DOS Monitor (`ADW30.386`) is missing or old;
DOS-session activity/hotkeys and Windows 95 SystemIQ will be unavailable.
It explicitly offers to continue loading. WineVDM stderr reports an unknown
VxD `326d`, consistent with that warning. We have not installed or emulated the
driver, and have not yet established whether normal preview needs it.

UI progress stopped because the current desktop was showing the owner's existing
Ocuvera screensaver and focusing the dialog failed with access denied. The user
was asked to dismiss the screensaver/unlock. The captured green animation was
**not After Dark evidence**; accessibility text identified the pending warning.

Current proof: original host startup and dialog creation. Not yet proven:
continuing past the warning, module selection, preview, fullscreen animation,
input dismissal or clean shutdown. No .NET frame transport or Ocuvera integration
has been attempted. Private provenance and logs are in the local experiment's
`report.json`, `original-inventory.json`, `stdout.log` and `stderr.log`.
