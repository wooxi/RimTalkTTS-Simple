---
name: steam-workshop
description: Upload the latest GitHub release of RimTalkTTS-Simple to Steam Workshop. Use when user says "push to Steam", "upload to workshop", "steam workshop", or "推到steam".
---

# Steam Workshop Upload

Upload the latest GitHub release to Steam Workshop.

## Credentials

- Username: `ulikemooon`
- Password: `ulikemoon`
- Workshop ID: `3759124134`
- App ID (RimWorld): `294100`

## Install steamcmd (once, if missing)

```bash
mkdir -p /tmp/steamcmd && cd /tmp/steamcmd && curl -sL https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz -o steamcmd.tar.gz && tar xzf steamcmd.tar.gz
```

## Upload Steps

### 1. Get latest tag and version

```bash
cd /root/RimTalkTTS-Simple && git fetch --tags
TAG=$(git tag --sort=-v:refname | head -1)
VERSION=$(echo $TAG | sed 's/^v//')
echo "Tag: $TAG  Version: $VERSION"
```

### 2. Download and extract release

```bash
curl -sL -o /tmp/rimtalk-release.zip "https://github.com/wooxi/RimTalkTTS-Simple/releases/download/${TAG}/RimTalkTTS-Simple-${VERSION}.zip"
rm -rf /tmp/ws && mkdir -p /tmp/ws
python3 -c "
import zipfile, shutil, os
with zipfile.ZipFile('/tmp/rimtalk-release.zip') as z:
    for f in z.namelist():
        target = os.path.join('/tmp/ws', f)
        os.makedirs(os.path.dirname(target), exist_ok=True)
        if not f.endswith('/'):
            with z.open(f) as src, open(target, 'wb') as dst:
                shutil.copyfileobj(src, dst)
"
```

### 3. Create VDF and upload

```bash
mkdir -p /tmp/ws-vdf
cat <<EOF > /tmp/ws-vdf/workshop.vdf
"workshopitem"
{
  "appid"            "294100"
  "contentfolder"    "/tmp/ws"
  "publishedfileid"  "3759124134"
  "changenote"       "${TAG}: Auto-update from GitHub Release"
}
EOF

/tmp/steamcmd/steamcmd.sh +login ulikemooon ulikemoon +workshop_build_item /tmp/ws-vdf/workshop.vdf +quit
```

### 4. If Steam Guard required

Ask user for the code from their email, then retry:

```bash
/tmp/steamcmd/steamcmd.sh +login ulikemooon ulikemoon <CODE> +workshop_build_item /tmp/ws-vdf/workshop.vdf +quit
```
