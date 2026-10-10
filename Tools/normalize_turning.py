#!/usr/bin/env python3
"""Rescales the majority-direction corner angles of a layout so the net turning is exactly +-360 degrees.
Usage: normalize_turning.py <file.json> [id ...]   (rewrites layouts in place, only for listed ids or all flagged)"""
import json, math, re, sys
path = sys.argv[1]
ids = set(sys.argv[2:])
text = open(path).read()
data = json.loads(text)
for c in data:
    toks = c['layout'].split()
    L = R = 0
    for t in toks:
        b = t.split(':')[0]
        if b[0] in 'LR':
            a = float(b[1:].split('@')[0])
            if b[0] == 'L': L += a
            else: R += a
    net = L - R
    if ids and c['id'] not in ids: continue
    if not ids and abs(abs(net) - 360) < 60: continue
    major = 'L' if net > 0 else 'R'
    f = (360 + (R if major == 'L' else L)) / (L if major == 'L' else R)
    out = []
    for t in toks:
        parts = t.split(':')
        b = parts[0]
        if b[0] == major:
            a, r = b[1:].split('@')
            a = max(25, round(float(a) * f))
            b = f"{major}{a}@{r}"
        out.append(':'.join([b] + parts[1:]))
    new = ' '.join(out)
    text = text.replace(c['layout'], new)
    print(c['id'], 'factor', round(f, 2))
open(path, 'w').write(text)
