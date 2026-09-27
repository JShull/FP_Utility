# Copyright (c) 2026 John B. Shull. See LICENSE.md.
"""Reproduce FP_StarRank's fixed 0.45-ratio, five-point star area coefficients.

Clip a scaled star polygon against the unit square. Between vertex/edge/corner
events its area is quadratic in scale. Differences in quadratic coefficients
give a compact sum of squared hinges, without a texture or polygon loop in HLSL.
This offline maintenance helper uses only Python's standard library.
"""
import math

points=[((1 if i%2==0 else .45)*math.sin(i*math.pi/5), (1 if i%2==0 else .45)*math.cos(i*math.pi/5)) for i in range(10)]
def area(r):
    poly=[(x*r,y*r) for x,y in points]
    for axis,sign in [(0,1),(0,-1),(1,1),(1,-1)]:
        out=[]
        for i,b in enumerate(poly):
            a=poly[i-1]; inside_a=a[axis]*sign<=.5; inside_b=b[axis]*sign<=.5
            if inside_a!=inside_b:
                t=(.5/sign-a[axis])/(b[axis]-a[axis])
                out.append((a[0]+t*(b[0]-a[0]),a[1]+t*(b[1]-a[1])))
            if inside_b:out.append(b)
        poly=out
    return abs(sum(poly[i-1][0]*p[1]-p[0]*poly[i-1][1] for i,p in enumerate(poly)))/2
def gauge(x,y):
    x=abs(x)
    for kx,ky in [(.8090169943749475,-.5877852522924731),(-.8090169943749475,-.5877852522924731)]:
        d=max(x*kx+y*ky,0);x-=2*d*kx;y-=2*d*ky
    return y+abs(x)*(1-.45*.8090169943749475)/(.45*.5877852522924731)
events=sorted(set(round(r,12) for r in [.5/abs(c) for p in points for c in p if abs(c)>1e-6]+[gauge(x,y) for x,y in [(.5,.5),(.5,-.5),(-.5,.5),(-.5,-.5)]]))
events=[r for r in events if r<=max(gauge(x,y) for x,y in [(.5,.5),(.5,-.5),(-.5,.5),(-.5,-.5)])+1e-9]
events.append(events[-1]+1)
curves=[]
for low,high in zip([0]+events,events):
    mid=(low+high)/2;h=(high-low)*.2
    curves.append((area(mid-h)+area(mid+h)-2*area(mid))/(2*h*h))
hinges=[(r,b-a) for r,a,b in zip(events,curves,curves[1:]) if abs(b-a)>1e-6]
print('initial',curves[0]);print('hinges',hinges)
def estimated(r):return curves[0]*r*r+sum(c*max(r-t,0)**2 for t,c in hinges)
print('maximum error',max(abs(estimated(i/1000*events[-2])-area(i/1000*events[-2])) for i in range(1001)))
