"""Generate a local example profile from existing Titan artifacts; no build/install."""
import argparse
from pathlib import Path
import zipfile
from cumod import digest, doctor, relative, write_new


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--game", required=True)
    p.add_argument("--deps", required=True)
    p.add_argument("--titan", required=True, help="Path to the existing TitanExosuit.dll")
    p.add_argument("--out", required=True)
    a = p.parse_args()
    env = doctor(a.game)
    deps = Path(a.deps).resolve()
    loader = deps / "BepInEx_win_x64_5.4.23.6.zip"
    core = deps / "CUCoreLib.dll"
    titan = Path(a.titan).resolve()
    with zipfile.ZipFile(loader) as archive:
        loader_files = [{"source": str(loader), "member": i.filename,
                         "target": relative(i.filename), "sha256": digest(archive.read(i))}
                        for i in archive.infolist() if not i.is_dir()]
    value = {
        "schema": 1, "game": "casualties-unknown", "name": "titan-local",
        "build": env["build"],
        "fingerprints": [{"source": str(Path(a.game).resolve() / "CasualtiesUnknown_Data/Managed/Assembly-CSharp.dll"), "sha256": env["assembly_sha256"]}],
        "enabled": ["bepinex.loader", "net.cucorelib", "com.testground.titanexosuit"],
        "components": [
            {"id": "bepinex.loader", "version": "5.4.23.6", "files": loader_files},
            {"id": "net.cucorelib", "version": "1.1", "requires": {"bepinex.loader": "5.4.23.6"},
             "files": [{"source": str(core), "target": "BepInEx/plugins/CUCoreLib.dll", "sha256": digest(core.read_bytes())}]},
            {"id": "com.testground.titanexosuit", "version": "0.1.0", "requires": {"net.cucorelib": "1.1"},
             "files": [{"source": str(titan), "target": "BepInEx/plugins/TitanExosuit/TitanExosuit.dll", "sha256": digest(titan.read_bytes())}]}
        ]
    }
    write_new(a.out, value)
    print(a.out)


if __name__ == "__main__":
    main()
