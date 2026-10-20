#!/usr/bin/env python3
"""Build and package the 1.7 prerelease without an installed game."""
import argparse
import hashlib
import io
from pathlib import Path
import re
import subprocess
import tempfile
import urllib.request
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
GAME_REFERENCES = '1.7.4982-unstable'
HARMONY = '2.4.2'


def references(package, version, prefix, destination):
    url = f'https://api.nuget.org/v3-flatcontainer/{package}/{version}/{package}.{version}.nupkg'
    with urllib.request.urlopen(url, timeout=60) as response:
        data = response.read()
    destination.mkdir(parents=True)
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        for name in archive.namelist():
            if name.startswith(prefix) and name.endswith('.dll'):
                (destination / Path(name).name).write_bytes(archive.read(name))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--version', required=True, help='Release tag, e.g. v0.2.0-pre.1')
    args = parser.parse_args()
    if not re.fullmatch(r'v\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?', args.version):
        parser.error('Expected a v-prefixed semantic version')
    mod_version = ET.parse(ROOT / 'About/About.xml').findtext('modVersion')
    if args.version[1:].split('-')[0] != mod_version:
        parser.error('Release version must match About/About.xml modVersion')

    with tempfile.TemporaryDirectory(prefix='hda-release-') as temp:
        temp = Path(temp)
        references('krafs.rimworld.ref', GAME_REFERENCES, 'ref/net10.0/', temp / 'game')
        references('lib.harmony', HARMONY, 'lib/net10.0/', temp / 'harmony')
        subprocess.run([
            'dotnet', 'build', str(ROOT / 'Source/HighDensityAnimals.csproj'), '-c', 'Release',
            f'-p:GameManagedPath={temp / "game"}',
            f'-p:HarmonyAssembly={temp / "harmony/0Harmony.dll"}',
        ], check=True)

    if [v.text for v in ET.parse(ROOT / 'About/About.xml').findall('supportedVersions/li')] != ['1.7']:
        raise RuntimeError('The release must advertise only RimWorld 1.7')
    folders = ET.parse(ROOT / 'LoadFolders.xml').getroot()
    if [v.tag for v in folders] != ['v1.7'] or [v.text for v in folders.findall('v1.7/li')] != ['/', '1.7']:
        raise RuntimeError('Unexpected version load folders')
    required = ['1.7/Assemblies/HighDensityAnimals.dll', 'About/About.xml',
                'Source/HighDensityAnimals.csproj', 'LoadFolders.xml', 'README.md', 'CHANGELOG.md', 'LICENSE']
    for name in required:
        if not (ROOT / name).is_file():
            raise RuntimeError(f'Missing package file: {name}')
    if not list((ROOT / '1.7/Defs').rglob('*.xml')) or (ROOT / 'Defs').exists():
        raise RuntimeError('Definitions must live in 1.7/Defs')

    output = ROOT / 'dist'
    output.mkdir(exist_ok=True)
    package = output / f'HighDensityAnimals-{args.version}.zip'
    files = [ROOT / name for name in ['LoadFolders.xml', 'README.md', 'CHANGELOG.md', 'LICENSE']]
    for folder in ['1.7', 'About', 'Languages', 'Source']:
        files.extend(p for p in (ROOT / folder).rglob('*') if p.is_file()
                     and not {'bin', 'obj'} & set(p.relative_to(ROOT / folder).parts))
    for path in files:
        if path.suffix == '.xml':
            ET.parse(path)
    with zipfile.ZipFile(package, 'w', zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(files):
            archive.write(path, 'HighDensityAnimals/' + path.relative_to(ROOT).as_posix())
    checksum = hashlib.sha256(package.read_bytes()).hexdigest()
    (output / 'SHA256SUMS').write_text(f'{checksum}  {package.name}\n')
    print(f'Packaged {len(files)} files: {package}\nSHA-256: {checksum}', flush=True)


if __name__ == '__main__':
    main()
