"""Doxygen input filter: decode legacy C# comments without changing source files."""
from pathlib import Path
import sys


def filtered_source(path):
    path = Path(path)
    data = path.read_bytes()
    if path.suffix.lower() == '.cs':
        try:
            data.decode('utf-8')
        except UnicodeDecodeError:
            # Strict conversion: do not discard or replace undecodable bytes.
            return data.decode('cp1252').encode('utf-8')
    return data


if __name__ == '__main__':
    sys.stdout.buffer.write(filtered_source(sys.argv[1]))
