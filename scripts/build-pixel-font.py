"""Build an unmodified glyph atlas from the official Fusion Pixel BDF, using Python stdlib.
Usage: python scripts/build-pixel-font.py path/to/zh_hans.bdf
The bundled OFL notices must accompany the generated atlas.
"""
import json, pathlib, struct, sys, zlib
source = pathlib.Path(sys.argv[1])
target = pathlib.Path(__file__).resolve().parents[1] / 'src/GameMod/Fonts'
target.mkdir(parents=True, exist_ok=True)
glyphs = []
for block in source.read_text(encoding='utf-8').split('STARTCHAR ')[1:]:
    lines = block.splitlines()
    fields = {line.split(' ', 1)[0]: line.split(' ', 1)[1] for line in lines if ' ' in line}
    code = int(fields['ENCODING'])
    if code < 0: continue
    w, h, bx, by = map(int, fields['BBX'].split())
    start = lines.index('BITMAP') + 1
    glyphs.append((code, w, h, bx, by, int(fields['DWIDTH'].split()[0]), lines[start:start+h]))
width, cellw, cellh = 4096, 16, 20
columns = width // cellw
height = ((len(glyphs) + columns - 1) // columns) * cellh
pixels = bytearray(width * height * 4)
entries = []
for i, (code, w, h, bx, by, advance, bitmap) in enumerate(glyphs):
    x, y = (i % columns)*cellw + 1, (i // columns)*cellh + 1
    for row, bits in enumerate(bitmap):
        raw = int(bits, 16)
        for col in range(w):
            if raw & (1 << (len(bits)*4-col-1)):
                offset = ((y+row)*width+x+col)*4
                pixels[offset:offset+4] = b'\xff\xff\xff\xff'
    entries.append(dict(code=code,w=w,h=h,bx=bx,by=by,advance=advance,x=x,y=height-y-h))
def chunk(kind, data):
    return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind+data))
scan = b''.join(b'\0'+pixels[y*width*4:(y+1)*width*4] for y in range(height))
png = b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>2I5B',width,height,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(scan,9))+chunk(b'IEND',b'')
(target/'fusion-pixel.png').write_bytes(png)
(target/'fusion-pixel.json').write_text(json.dumps(dict(width=width,height=height,glyphs=entries),separators=(',',':')),encoding='utf-8')
(target/'fusion-pixel.bin').write_bytes(struct.pack('<3i',width,height,len(entries))+b''.join(struct.pack('<9i',*(e[k] for k in ('code','w','h','bx','by','advance','x','y')),0) for e in entries))
print(f'{len(entries)} glyphs; atlas {width}x{height}; {len(png)} bytes PNG')
