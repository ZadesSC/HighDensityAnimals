#!/usr/bin/env python3
"""Build and run the 1.7 regression mod in disposable game profiles."""
import argparse
from collections import Counter
from datetime import datetime
import hashlib
import json
import math
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time
import uuid
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
PREPARE_CASES = {
    'package', 'egg_stacks', 'admission', 'pair_selection', 'blocked_ejection', 'lifecycle',
    'fusion_and_pause', 'post_cycle_partner', 'density_waste', 'cap_and_cancel', 'nutrition', 'production', 'combat', 'offspring', 'natural_offspring', 'rendering', 'settings',
    'localization_and_ui', 'reachability', 'negative_controls', 'autonomous_cycles', 'interrupted_haul', 'save_fixture'
}
RESUME_CASES = {'fresh_process_reload'}


def validate_results(rows, expected, artifact_hash):
    """COMPLETE alone never implies success. Enforce the independently maintained manifest."""
    starts = [r for r in rows if r.get('type') == 'start']
    cases = [r for r in rows if r.get('type') == 'case']
    counts = Counter(r.get('id') for r in cases)
    problems = []
    if len(starts) != 1 or set(starts[0].get('expected', [])) != expected:
        problems.append('Missing or incorrect case manifest')
    if len(starts) == 1 and starts[0].get('artifactHash') != artifact_hash:
        problems.append('Loaded DLL hash differs from freshly built artifact')
    if set(counts) != expected or any(count != 1 for count in counts.values()):
        problems.append(f'Missing, duplicate, or unexpected cases: {dict(counts)}')
    for row in cases:
        if row.get('passed') is not True:
            problems.append(f"{row.get('id')}: {row.get('error', 'assertion failed')}")
    problems.extend(str(r) for r in rows if r.get('type') in {'fatal', 'game_error'})
    if sum(r.get('type') == 'complete' for r in rows) != 1:
        problems.append('Run did not finish exactly once')
    if expected == {'benchmark'}:
        samples = [r for r in rows if r.get('type') == 'benchmark']
        workloads = Counter((r.get('pawns'), r.get('density'), r.get('machines')) for r in samples)
        if workloads != Counter({(120, 1, 0): 1, (30, 4, 0): 1, (30, 4, 100): 1}):
            problems.append('Missing/duplicate performance workload')
        for sample in samples:
            times = sample.get('milliseconds', [])
            allocations = sample.get('allocations', [])
            if sample.get('ticksPerSample') != 1000 or len(times) != 6 or len(allocations) != 6 or \
                    any(not isinstance(t, (int, float)) or not math.isfinite(t) or t <= 0 for t in times) or \
                    any(not isinstance(n, int) or n < 0 for n in allocations):
                problems.append('Invalid performance samples')
    return problems


def write_xml(path, tag, values):
    root = ET.Element(tag)
    for key, value in values.items():
        el = ET.SubElement(root, key)
        if isinstance(value, list):
            for item in value: ET.SubElement(el, 'li').text = item
        else: el.text = str(value)
    ET.indent(root)
    ET.ElementTree(root).write(path, encoding='utf-8', xml_declaration=True)


def read_rows(path):
    if not path.exists(): return []
    return [json.loads(line) for line in path.read_text().splitlines() if line.strip()]


def run_phase(exe, game, profile, report, phase, content, prepare_report, artifact_hash, timeout):
    env = dict(os.environ, HDA_TEST_REPORT=str(report), HDA_TEST_PHASE=phase,
               HDA_TEST_CONTENT=content, HDA_TEST_PREPARE_REPORT=str(prepare_report))
    args = [str(exe), f'-savedatafolder={profile}']
    if phase in {'prepare', 'benchmark'}: args.append('-quicktest')
    log_path = report.with_suffix('.log')
    seen = 0
    with log_path.open('w') as log:
        process = subprocess.Popen(args, cwd=game, env=env, stdout=log, stderr=subprocess.STDOUT)
        deadline = time.monotonic() + timeout
        try:
            while time.monotonic() < deadline:
                rows = read_rows(report)
                for row in rows[seen:]:
                    if row.get('type') == 'case':
                        print(f"  {'PASS' if row['passed'] else 'FAIL'} {row['id']}", flush=True)
                seen = len(rows)
                if any(r.get('type') in {'complete', 'fatal', 'game_error'} for r in rows): break
                if process.poll() is not None: break
                time.sleep(.5)
        finally:
            if process.poll() is None:
                process.terminate()
                try: process.wait(timeout=10)
                except subprocess.TimeoutExpired: process.kill(); process.wait()
    rows = read_rows(report)
    problems = validate_results(rows, PREPARE_CASES if phase == 'prepare' else ({'benchmark'} if phase == 'benchmark' else ({'legacy_save_migration'} if phase == 'legacy' else RESUME_CASES)), artifact_hash)
    log_text = re.sub(r'\x1b\[[0-9;]*m', '', log_path.read_text(errors='replace'))
    if re.search(r'\bERROR\b|Unhandled exception|Exception in static constructor', log_text):
        problems.append(f'Game startup/runtime error in {log_path}')
    if not any(r.get('type') in {'complete', 'fatal', 'game_error'} for r in rows):
        problems.append('Game exited early or timed out')
    if problems: raise RuntimeError('\n'.join(problems))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game-dir', type=Path, default=Path.home() / 'Library/Application Support/Steam/steamapps/common/RimWorld')
    parser.add_argument('--dotnet', default=shutil.which('dotnet') or 'dotnet')
    parser.add_argument('--harmony', type=Path)
    parser.add_argument('--content', choices=['core', 'full', 'both'], default='both')
    parser.add_argument('--results-dir', type=Path)
    parser.add_argument('--benchmark-only', action='store_true', help='Run only the isolated performance workloads')
    parser.add_argument('--benchmark', action='store_true', help='Also collect tick-time/allocation samples for equal-production animal populations and 100 idle machines')
    parser.add_argument('--language', default='English')
    parser.add_argument('--timeout', type=int, default=240)
    args = parser.parse_args()
    game = args.game_dir.resolve()
    managed = game / 'RimWorld.app/Contents/MacOS'
    exe = managed / 'RimWorld'
    if not exe.exists(): parser.error('This 1.7 runtime runner currently requires the macOS Waylith build')
    harmony = args.harmony or game.parent.parent / 'workshop/content/294100/2009463077/1.7/Assemblies/0Harmony.dll'
    if not harmony.exists(): parser.error('Pass --harmony pointing to the 1.7 0Harmony.dll')
    results = (args.results_dir or ROOT / 'tmp_tools/test-runs' / (datetime.now().strftime('%Y%m%d-%H%M%S') + '-' + uuid.uuid4().hex[:8])).resolve()
    results.mkdir(parents=True, exist_ok=False)
    build = [args.dotnet, 'build', str(ROOT / 'tests/runtime/RuntimeTests.csproj'), '-c', 'Release',
             f'-p:RimWorldDir={game}', f'-p:GameManagedPath={managed}', f'-p:HarmonyAssembly={harmony}']
    subprocess.run(build, check=True)
    mod = results / 'mod'
    for folder in ['About', 'Languages', '1.7']:
        shutil.copytree(ROOT / folder, mod / folder)
    shutil.copy2(ROOT / 'LoadFolders.xml', mod / 'LoadFolders.xml')
    harness = results / 'harness'
    shutil.copytree(ROOT / 'tests/runtime/About', harness / 'About')
    (harness / 'Assemblies').mkdir()
    shutil.copy2(ROOT / 'tests/runtime/bin/RuntimeTests.dll', harness / 'Assemblies/RuntimeTests.dll')
    artifact = mod / '1.7/Assemblies/HighDensityAnimals.dll'
    artifact_hash = hashlib.sha256(artifact.read_bytes()).hexdigest()
    manifest = {'artifactHash': artifact_hash, 'sourceCommit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
                'gameVersion': (game / 'Version.txt').read_text().strip(), 'content': args.content,
                'harmonyHash': hashlib.sha256(harmony.read_bytes()).hexdigest(),
                'sourceHashes': {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest()
                    for folder in ['Source', '1.7/Defs', 'Languages', 'tests/runtime']
                    for p in (ROOT / folder).rglob('*') if p.is_file() and not {'bin', 'obj'} & set(p.parts)}}
    (results / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
    links = []
    try:
        for name, path in [('HDA-regression-mod', mod), ('HDA-regression-harness', harness)]:
            link = game / 'Mods' / (name + '-' + results.name)
            link.symlink_to(path, target_is_directory=True); links.append(link)
        for content in ['core', 'full'] if args.content == 'both' else [args.content]:
            print(f'Testing {content} content; results: {results}', flush=True)
            profile = results / content
            (profile / 'Config').mkdir(parents=True)
            (profile / 'Saves').mkdir()
            mods = ['brrainz.harmony', 'ludeon.rimworld']
            if content == 'full':
                mods.extend(['ludeon.rimworld.royalty', 'ludeon.rimworld.ideology', 'ludeon.rimworld.biotech', 'ludeon.rimworld.anomaly', 'ludeon.rimworld.odyssey'])
            mods.extend(['zades.highdensityanimals', 'local.hda.regression'])
            write_xml(profile / 'Config/ModsConfig.xml', 'ModsConfigData', {'version': manifest['gameVersion'].split()[0], 'activeMods': mods, 'knownExpansions': ['ludeon.rimworld.royalty', 'ludeon.rimworld.ideology', 'ludeon.rimworld.biotech', 'ludeon.rimworld.anomaly', 'ludeon.rimworld.odyssey']})
            write_xml(profile / 'Config/Prefs.xml', 'PrefsData', {'screenWidth': 1280, 'screenHeight': 800, 'fullscreen': 'False',
                'runInBackground': 'True', 'devMode': 'True', 'pauseOnError': 'False', 'pauseOnLoad': 'True',
                'openLogOnWarnings': 'False', 'volumeGame': 0, 'volumeMusic': 0, 'langFolderName': args.language})
            if args.benchmark_only:
                benchmark = results / f'{content}-benchmark.jsonl'
                run_phase(exe, game, profile, benchmark, 'benchmark', content, benchmark, artifact_hash, args.timeout)
                continue
            prepare = results / f'{content}-prepare.jsonl'
            run_phase(exe, game, profile, prepare, 'prepare', content, prepare, artifact_hash, args.timeout)
            shutil.copy2(profile / 'Saves/HdaRegression.rws', profile / 'Saves/autostart.rws')
            resume = results / f'{content}-resume.jsonl'
            run_phase(exe, game, profile, resume, 'resume', content, prepare, artifact_hash, args.timeout)
            tree = ET.parse(profile / 'Saves/HdaRegression.rws')
            saved = tree.getroot()
            tick = int(saved.find('.//tickManager/ticksGame').text)
            snapshot = next(r for r in read_rows(prepare) if r.get('type') == 'snapshot')
            building = next((b for b in saved.findall(".//thing[@Class='HighDensityAnimals.Building_AnimalDensifier']")
                             if b.findtext('id') == f"HDA_AnimalDensifier{snapshot['machine']}"), None)
            if building is None: raise RuntimeError('No densifier in persistence fixture')
            for key in ['cycleTicksRemaining', 'cyclePrimary', 'cycleSecondary', 'cyclePrimaryDensity', 'cycleSecondaryDensity', 'cycleNutrition', 'cycleResultDensity', 'preventDensityWaste']:
                el = building.find(key)
                if el is not None: building.remove(el)
            end = building.find('cycleEndTick')
            if end is None: end = ET.SubElement(building, 'cycleEndTick')
            end.text = str(tick + snapshot['remaining'])
            tree.write(profile / 'Saves/autostart.rws', encoding='utf-8', xml_declaration=True)
            legacy = results / f'{content}-legacy.jsonl'
            run_phase(exe, game, profile, legacy, 'legacy', content, prepare, artifact_hash, args.timeout)
            if args.benchmark:
                benchmark = results / f'{content}-benchmark.jsonl'
                benchmark_profile = results / f'{content}-benchmark-profile'
                shutil.copytree(profile / 'Config', benchmark_profile / 'Config')
                (benchmark_profile / 'Saves').mkdir()
                run_phase(exe, game, benchmark_profile, benchmark, 'benchmark', content, prepare, artifact_hash, args.timeout)
        print(f'All runtime cases passed. Evidence: {results}', flush=True)
    finally:
        for link in links:
            if link.is_symlink(): link.unlink()


if __name__ == '__main__':
    try: main()
    except (RuntimeError, subprocess.CalledProcessError, OSError, ValueError) as error:
        print(f'FAILED: {error}', file=sys.stderr); sys.exit(1)
