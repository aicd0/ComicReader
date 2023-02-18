import os

key_file = "./ComicReader/Keys.cs"

def toCamelCase(name: str) -> str:
    words = name.split('_')
    if len(words) == 0:
        return name
    return ''.join(i.capitalize() for i in words)

def main():
    keys = ["APP_SECRET"]
    key_maps = {}
    for k in keys:
        v = os.environ[k]
        if v is None:
            continue
        key_maps[toCamelCase(k)] = v
    with open(key_file, 'rb+') as f:
        content = f.read().decode()
        for k, v in key_maps.items():
            old_str = k + ' = ""'
            new_str = k + ' = "' + v + '"'
            content = content.replace(old_str, new_str)
        f.seek(0)
        f.truncate()
        f.write(content.encode())

if __name__ == "__main__":
    print("pre-build.py start")
    main()
    print("pre-build.py end")
