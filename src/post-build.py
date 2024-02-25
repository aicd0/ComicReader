import os
import shutil
import subprocess

def get_msix_file(dir: str) -> str:
    files = os.listdir(dir)
    for file in files:
        if file.endswith('.msix'):
            return file
    raise AssertionError()

def run_command(commands: list[str]):
    p = subprocess.Popen(commands, stdout=subprocess.STDOUT)
    p.communicate()
    print("command '%s' returned with code %d." % (' '.join(commands), p.returncode))
    if p.returncode != 0:
        raise AssertionError()
    p.returncode

def main():
    arm64dir = './ComicReader/bin/ARM64/Release/net8.0-windows10.0.22621.0/win-arm64/AppPackages/'
    x64dir = './ComicReader/bin/x64/Release/net8.0-windows10.0.22621.0/win-x64/AppPackages/'
    x86dir = './ComicReader/bin/x86/Release/net8.0-windows10.0.22621.0/win-x86/AppPackages/'
    name = os.listdir(arm64dir)[0]
    outputdir = name + '/'
    arm64outputdir = outputdir + name + '_arm64/'
    x64outputdir = outputdir + name + '_x64/'
    x86outputdir = outputdir + name + '_x86/'
    shutil.copytree(arm64dir + outputdir, arm64outputdir)
    shutil.copytree(x64dir + outputdir, x64outputdir)
    shutil.copytree(x86dir + outputdir, x86outputdir)
    tmpdir = outputdir + 'temp/'
    arm64msixfile = get_msix_file(arm64outputdir)
    x64msixfile = get_msix_file(x64outputdir)
    x86msixfile = get_msix_file(x86outputdir)
    os.makedirs(tmpdir)
    shutil.copyfile(arm64outputdir + arm64msixfile, tmpdir + arm64msixfile)
    shutil.copyfile(x64outputdir + x64msixfile, tmpdir + x64msixfile)
    shutil.copyfile(x86outputdir + x86msixfile, tmpdir + x86msixfile)

if __name__ == '__main__':
    main()