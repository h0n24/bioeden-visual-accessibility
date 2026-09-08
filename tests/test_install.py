"""Run against a throwaway copy of Managed; never starts or alters the live game."""
import hashlib, json, pathlib, shutil, subprocess, tempfile, zipfile

root = pathlib.Path(__file__).resolve().parents[1]
live = pathlib.Path(r'D:\Games\BioEden\BioEden_Data\Managed')
manifest = json.loads((root/'manifest.json').read_text())
fixture = pathlib.Path(tempfile.mkdtemp(prefix='v121-test-', dir=root/'build'))
managed = fixture/'BioEden_Data/Managed'
shutil.copytree(live,managed)
(fixture/'BioEden.exe').touch()
hash_file = lambda p: hashlib.sha256(p.read_bytes()).hexdigest().upper()
checks = []
names = [f['name'] for f in manifest['files']] + ['BioEden.NoDOF.dll']

def run(action, success=True):
    r = subprocess.run(['powershell.exe','-NoProfile','-ExecutionPolicy','Bypass','-File',str(root/'NoDOF.ps1'),'-Action',action,'-GamePath',str(fixture)],capture_output=True,text=True)
    assert (r.returncode == 0) == success, r.stdout+r.stderr
    return r.stdout

initial_version = 'previous installed version (hashes captured in report)'
if hash_file(managed/'Assembly-CSharp.dll') == manifest['files'][0]['patched']:
    # A repeat test run starts from original copies instead of an already installed
    # package, so the forced failure actually exercises the transaction.
    for f in manifest['files']:
        shutil.copyfile(managed/(f['name']+'.NoDOF.original'),managed/f['name'])
    (managed/'BioEden.NoDOF.dll').unlink()
    initial_version = 'original'
snapshot = lambda: {name:hash_file(managed/name) if (managed/name).exists() else None for name in names}
before = snapshot()
# Deny replacing the second target to force failure after runtime and first library.
with open(managed/'Unity.RenderPipelines.Universal.Runtime.dll','rb'):
    run('Install',False)
assert snapshot() == before
checks.append('Forced replacement failure after partial upgrade rolls back both changed files to prior version.')
run('Install')
for f in manifest['files']:
    assert hash_file(managed/f['name']) == f['patched']
    assert hash_file(managed/(f['name']+'.NoDOF.original')) == f['original']
assert hash_file(managed/'BioEden.NoDOF.dll') == manifest['runtime']
checks.append(f'Install from {initial_version}: exact output hashes and untouched original backups.')
run('Install');run('Status')
checks.append('Repeated installation is idempotent and status succeeds.')
run('Uninstall');run('Uninstall')
for f in manifest['files']: assert hash_file(managed/f['name']) == f['original']
assert not (managed/'BioEden.NoDOF.dll').exists()
checks.append('Uninstall restores original libraries exactly, removes runtime, and is idempotent.')
run('Install')
checks.append('Clean-original installation succeeds.')
target = managed/'Assembly-CSharp.dll'; original=target.read_bytes()
target.write_bytes(original[:-1]+bytes([original[-1]^1])); changed=target.read_bytes()
run('Install',False);run('Uninstall',False);assert target.read_bytes()==changed
target.write_bytes(original)
checks.append('Unknown game library is rejected without changing it.')
backup=managed/'Assembly-CSharp.dll.NoDOF.original'; original=backup.read_bytes()
backup.write_bytes(b'Invalid backup');run('Uninstall',False);backup.write_bytes(original)
checks.append('Corrupted original backup blocks uninstall.')
runtime=managed/'BioEden.NoDOF.dll'; original=runtime.read_bytes()
runtime.write_bytes(b'Unknown runtime');run('Uninstall',False);assert runtime.read_bytes()==b'Unknown runtime';runtime.write_bytes(original)
checks.append('Unknown runtime is preserved and rejected.')
run('Uninstall')
# Reconstruct the verified v1 byte patch to test direct upgrade from v1.0.
legacy=json.loads((root/'tests/legacy-v1.json').read_text())
import base64
target=managed/'Unity.RenderPipelines.Universal.Runtime.dll'
data=bytearray(target.read_bytes()); chunk=base64.b64decode(legacy['patchedBytes'])
data[legacy['offset']:legacy['offset']+len(chunk)]=chunk;target.write_bytes(data)
assert hash_file(target)==legacy['patchedSha256']
run('Install');run('Uninstall')
checks.append('Direct upgrade from v1.0 fixed-Off patch and subsequent full uninstall succeed.')
report={'result':'PASS','version':manifest['version'],'initialHashes':before,'checks':checks,'fixture':str(fixture),'inGameTests':'User will test manually.'}
(root/'test-evidence/installer-tests-v1.2.1.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
