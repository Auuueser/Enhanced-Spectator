"""Prepare pinned Unity runtime source in ignored libs/, never in the source tree."""
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import re
import tarfile
import urllib.request


def prepare():
    tool_dir = Path(__file__).resolve().parent
    cache = tool_dir.parent.parent / "libs" / "camera-agent"
    cache.mkdir(parents=True, exist_ok=True)
    packages = json.loads((tool_dir / "dependencies.lock.json").read_text(encoding="utf-8"))["packages"]
    for package in packages:
        name, version = package["name"], package["version"]
        archive = cache / (name + "-" + version + ".tgz")
        if not archive.exists():
            with urllib.request.urlopen(package["url"], timeout=60) as response:
                data = response.read()
            actual = hashlib.sha256(data).hexdigest()
            if actual != package["sha256"]:
                raise ValueError(f"{name} {version}: unexpected package SHA-256 {actual}")
            archive.write_bytes(data)
        data = archive.read_bytes()
        actual = hashlib.sha256(data).hexdigest()
        if actual != package["sha256"]:
            raise ValueError(f"{archive}: unexpected cached SHA-256 {actual}")
        destination = cache / "sources" / name
        source_hash = hashlib.sha256()
        with tarfile.open(fileobj=io.BytesIO(data), mode="r:gz") as source:
            for member in source.getmembers():
                path = PurePosixPath(member.name)
                if not member.isfile() or not path.parts or path.parts[0] != "package":
                    continue
                relative = PurePosixPath(*path.parts[1:])
                if ".." in relative.parts or relative.is_absolute():
                    raise ValueError(f"Unsafe package path: {member.name}")
                is_runtime = relative.parts[0] == "Runtime" and relative.suffix == ".cs"
                is_notice = relative.as_posix() in ("LICENSE.md", "Third Party Notices.md", "package.json")
                if not (is_runtime or is_notice):
                    continue
                content = source.extractfile(member).read()
                if is_runtime:
                    text = content.decode("utf-8-sig")
                    text = re.sub(r"\b" + re.escape(package["namespace"]) + r"\b", package["privateNamespace"], text)
                    if name == "com.unity.cinemachine" and relative.as_posix() == "Runtime/Core/CinemachineCore.cs":
                        # Our caller owns a simulation tick. Keep Unity's default
                        # outside an explicit tick; never modify Unity Time itself.
                        anchor = "public static float UniformDeltaTimeOverride = -1;"
                        if text.count(anchor) != 1 or text.count("Time.frameCount") != 4:
                            raise ValueError("Pinned Cinemachine clock patch no longer matches its exact source")
                        text = text.replace("Time.frameCount", "FrameCount")
                        text = text.replace(anchor, anchor + "\n\n        /// <summary>Private Camera Agent caller clock; -1 uses Unity's real frame.</summary>\n        public static int FrameCountOverride = -1;\n        internal static int FrameCount => FrameCountOverride >= 0 ? FrameCountOverride : Time.frameCount;")
                    content = text.encode("utf-8")
                    source_hash.update(relative.as_posix().encode("utf-8") + b"\0" + content)
                target = destination / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                if not target.exists() or target.read_bytes() != content:
                    target.write_bytes(content)
        prepared_hash = source_hash.hexdigest()
        if prepared_hash != package["preparedRuntimeSha256"]:
            raise ValueError(f"{name}: unexpected prepared runtime SHA-256 {prepared_hash}")
        manifest = {"package": name, "version": version, "archiveSha256": actual,
                    "preparedRuntimeSha256": prepared_hash,
                    "transform": "private-namespace-v1;cinemachine-caller-frame-v1" if name == "com.unity.cinemachine" else "private-namespace-v1"}
        (destination / "prepared-source.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
        print(f"{name} {version}: SHA-256 verified; private runtime source prepared")


if __name__ == "__main__":
    prepare()
