#!/usr/bin/env python3
"""Quick sanity check for circuit layout DSL strings (net turning, straight scaling)."""
import json, math, sys, glob, os
root = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'MotoManager', 'Resources', 'Data', 'circuits')
for path in sorted(glob.glob(os.path.join(root, '*.json'))):
    data = json.load(open(path))
    for c in (data if isinstance(data, list) else [data]):
        if not c.get('layout'):
            print(f"{c['id']:<16} real centre line ({len(c.get('shape', []))} points), layout derived in code")
            continue
        corners = straights = turn = 0
        n = 0
        for tok in c['layout'].split():
            tok = tok.split(':')[0]
            if tok[0] == 'S':
                straights += float(tok[1:])
            else:
                ang, rad = tok[1:].split('@')
                ang, rad = float(ang), float(rad)
                corners += rad * math.radians(ang)
                turn += ang if tok[0] == 'L' else -ang
                n += 1
        k = (c['lengthM'] - corners) / straights
        flag = '' if 0.75 < k < 1.33 and abs(abs(turn) - 360) < 60 else '  <-- check'
        print(f"{c['id']:<16} len {c['lengthM']:5.0f} corners {n:2d} cornerLen {corners:5.0f} straightScale {k:4.2f} turn {turn:5.0f}{flag}")
