# Copying After Dark files out of a Windows 98 VirtualBox VM

Use **7-Zip on the modern Windows host to extract files from the VM's virtual
hard disk after Windows 98 has shut down normally**. This does not require
clipboard integration, shared folders, network setup, or software installed
inside the guest.

```text
Windows 98 files
    -> VirtualBox disk image (.vdi) on the host
    -> 7-Zip opens the disk and its filesystem
    -> extract into this repository's ignored ad/<collection>/ directory
```

The VDI is a container for a virtual disk, including partitions and filesystems.
7-Zip can read both VDI containers and FAT filesystems. It extracts copies of
the files; this procedure does not mount the guest disk for writing.
See [7-Zip's supported formats](https://www.7-zip.org/).

## Setup observed on 2026-09-20

These are discovery results, not permanent assumptions. Recheck them before
the next transfer, especially the running state and snapshots.

| Item | Observed value |
| --- | --- |
| Registered VM name | `windows98` |
| VM directory, relative to the host user's profile | `Documents\VBVMs\windows98` |
| Attached hard disk | `windows98.vdi` in that directory |
| After Dark installation inside the guest | `C:\AFTERDRK` (owner-supplied path, verified in the disk image) |
| Snapshots | None at the time of inspection |
| Host tools | VirtualBox and 7-Zip installed under their usual `Program Files` directories |
| VM state | Initially running; subsequently verified powered off before extraction |

The setup was checked with VirtualBox's read-only inspection commands. After
normal guest shutdown, 7-Zip 26.01 successfully read VDI -> MBR -> FAT32 and
extracted the installation. The local destination is
`ad/windows98-2026-09-20/AFTERDRK/`. Two helper DLLs from the guest's Windows
directories were also copied, preserving their paths alongside AFTERDRK.
See the [verified collection inventory](research/windows98-collection-inventory.md)
for counts, hashes and the boundary between extraction and compatibility.

## Transfer steps

1. **Shut down Windows 98 from its Start menu.** Wait until VirtualBox reports
   **Powered Off**. Saving the VM's running state is not a substitute for a
   clean filesystem shutdown. Do not forcibly power it off for this procedure.
2. Open **7-Zip File Manager** on the host and open the VM's attached `.vdi`
   file. The observed location is under your profile at
   `Documents\VBVMs\windows98\windows98.vdi`; use VirtualBox's Storage settings
   or the commands below to confirm the actual path.
3. Browse into the partition/filesystem entry if 7-Zip first displays an image
   or partition instead of folders. Continue until the guest's directories are
   visible.
4. Locate the After Dark installation (`C:\AFTERDRK` in this guest) and extract
   its folders into a **new collection subfolder** under the repository's root
   `ad/` directory. Keep
   different releases separate so identically named modules do not overwrite
   one another. Preserve the original directory structure and filenames.
5. Include accompanying helper DLLs and data files, especially any `AD_RSRC`,
   `AD_SND`, or `WIN87EM` files. If they are absent from the installation
   directory, also check the guest's Windows and Windows\System directories.
   Their presence and compatibility are not guaranteed; preserve what is there.
   In the verified transfer, `AD_RSRC.DLL` was in `C:\WINDOWS`, `WIN87EM.DLL`
   was in `C:\WINDOWS\SYSTEM`, and `AD_SND.DLL` was in `C:\AFTERDRK`.
6. Check that the extracted files can be listed on the host. Record counts and
   SHA-256 hashes when inventorying the collection. A copied file is not yet a
   supported module: inspect its executable format and imports before attempting
   playback.

For example, with a checkout at `C:\repos\after-darker`, a destination could be:

```text
C:\repos\after-darker\ad\additional-collection\
```

The whole root `ad/` tree is already ignored, including nested directories and
supporting files of any extension. Do not force-add these inputs, extracted
images, or virtual disks to Git. Public notes should contain factual findings
and hashes rather than personal paths or proprietary file contents.

## Preserve different versions and original identities

The owner ran both the v2 and v3 installers in the guest and warned that an
installer may have replaced earlier files. Both old and updated versions are
valid research inputs; preserve every distinct version that is available.

- Extract each import into a new collection directory, retaining the guest's
  subdirectories and original filenames. Never overwrite an earlier collection
  merely because a module or helper DLL has the same name.
- Compare SHA-256 hashes to distinguish identical copies from different binary
  versions. Matching names do not establish identity, and different hashes do
  not by themselves establish which file is newer.
- Prefer separate collection directories over renaming binaries; helper lookup
  or module data references may depend on filenames. If a flat destination is
  necessary, use a collision-checked name such as `MODULE__sha256-<hash-prefix>.AD`
  and record its original guest path and full hash in the local inventory.
- Treat folder labels such as AD20 and AD30 as provenance, not proof of a
  pristine installer version. A later extraction cannot recover bytes already
  overwritten in the guest; preserve earlier host copies and consult original
  installation media separately if a missing historical version is needed.

In the verified transfer, all 29 extracted AD20 modules exactly match the
previously held modules by SHA-256. Both copies remain intact in separate
locations. This does not prove that every helper or other guest file survived
both installers unchanged.

## Read-only discovery commands for a future session

Run these in PowerShell on the host. Adjust the executable path if VirtualBox
is installed elsewhere, and replace the VM name with one returned by `list vms`.
These commands inspect configuration; they do not stop or change the VM.

```powershell
$virtualBoxManage = Join-Path $env:ProgramFiles 'Oracle\VirtualBox\VBoxManage.exe'

& $virtualBoxManage list vms
& $virtualBoxManage showvminfo 'windows98' --machinereadable |
    Select-String -Pattern 'VMState=|CfgFile=|\.vdi|\.vhd|\.vmdk|Snapshot'
& $virtualBoxManage snapshot 'windows98' list --machinereadable
```

Use the disk attached to the current VM configuration. Do not infer its location
solely from the VM's display name or the dated observations above. Keep raw
configuration output local because it includes host-specific paths.

## If snapshots exist or 7-Zip cannot open the disk

Snapshots can place newer writes in **differencing images** instead of the
original VDI. Opening only the base image may show old files, and a differencing
image is not a complete standalone disk. Inspect the chain before extracting.
If necessary, use VirtualBox's cloning facilities to create a separate,
self-contained copy of the current disk state, then inspect that copy. Preserve
the original VM and its snapshots; do not delete snapshots merely to transfer
files. See [VirtualBox virtual storage, differencing images and cloning](https://docs.oracle.com/en/virtualization/virtualbox/7.1/user/storage.html).

If 7-Zip reports an unsupported image or cannot expose the filesystem, retain
the error and inspect the actual disk format and snapshot state. Do not format,
repair, or alter the guest disk to make extraction work.

## Why supporting files matter to this project

Additional modules may reuse the Win16 drawing and resource services already
implemented. Their accompanying helper libraries could also clarify unresolved
dependencies. In particular, decoding an embedded bitmap does not implement the
proprietary `AD_RSRC` function contracts. Finding that helper would provide new
evidence to inspect, not automatic compatibility or permission to redistribute it.

After extraction, begin with the [NE inspection tutorial](tutorials.md) and the
[current runtime proof boundary](current-status.md). Keep collection inventory,
format identification, import analysis, and actual playback evidence distinct.
