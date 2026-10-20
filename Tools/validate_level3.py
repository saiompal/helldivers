"""Non-Unity geometry and scene integrity checks. Not an Editor compilation/playtest."""
from pathlib import Path
import json,math,re,hashlib,collections
ROOT=Path(__file__).resolve().parents[1]
A=ROOT/'Assets'
L=json.loads((A/'Resources/Level3RingLayout.json').read_text())
R=.275

def corners(b):
 a=math.radians(b.get('angle',0));c,s=math.cos(a),math.sin(a)
 return [(b['x']+x*c-y*s,b['y']+x*s+y*c) for x,y in [(-b['w']/2,-b['h']/2),(b['w']/2,-b['h']/2),(b['w']/2,b['h']/2),(-b['w']/2,b['h']/2)]]

def overlap(a,b):
 for polygon in (a,b):
  for i,p in enumerate(polygon):
   q=polygon[(i+1)%len(polygon)];n=(-(q[1]-p[1]),q[0]-p[0])
   aa=[x*n[0]+y*n[1] for x,y in a];bb=[x*n[0]+y*n[1] for x,y in b]
   if max(aa)<=min(bb)+1e-7 or max(bb)<=min(aa)+1e-7:return False
 return True

def point_clear(x,y,r=R):
 for b in L['blocks']:
  a=math.radians(-b.get('angle',0));dx=x-b['x'];dy=y-b['y']
  lx=dx*math.cos(a)-dy*math.sin(a);ly=dx*math.sin(a)+dy*math.cos(a)
  ex=max(abs(lx)-b['w']/2,0);ey=max(abs(ly)-b['h']/2,0)
  if ex*ex+ey*ey<r*r-1e-8:return False
 return True

def test_lift_sweeps():
 for lift in L['lifts']:
  swept=dict(x=lift['x'],y=lift['y'],w=lift['w'],h=2*lift['range']+lift['h'])
  for block in L['blocks']:
   assert not overlap(corners(swept),corners(block)),(lift['name'],'hits',block['name'])
 # Full half-turns preserve these relative clearances; turns pause all movers.
 motion=(A/'Scripts/Level3MotionSurface.cs').read_text()
 assert 'body.simulated = false' in motion and 'flip.IsTurning' in motion
 assert 'CanPlayerMove' in motion

def test_stars_and_spawn_clear():
 assert len(L['stars'])==5
 assert len({s['name'].split(' / ')[0] for s in L['stars']})==5
 for s in L['stars']:assert point_clear(s['x'],s['y'],.42),s['name']
 assert point_clear(L['spawnX'],L['spawnY'])
 assert point_clear(L['exitX'],L['exitY'])
 assert all(abs(b['angle'])>=10 for b in L['blocks'] if b['kind']=='conveyor')
 assert all('roller' not in b['name'].lower() for b in L['blocks'])

def test_physical_connectivity():
 # Flood ball-center free space at 0.2u resolution (no gravity/jump assumption).
 # Detect sealed sectors/exit, not a proof that a player can jump the route.
 step=.2;xs=[-23+step*i for i in range(231)];ys=[-11+step*i for i in range(111)]
 free={(i,j) for i,x in enumerate(xs) for j,y in enumerate(ys) if point_clear(x,y,R+.02)}
 def near(x,y):
  p=(round((x+23)/step),round((y+11)/step));assert p in free,(x,y,p);return p
 start=near(L['spawnX'],L['spawnY']);q=collections.deque([start]);seen={start}
 while q:
  i,j=q.popleft()
  for p in [(i+1,j),(i-1,j),(i,j+1),(i,j-1)]:
   if p in free and p not in seen:seen.add(p);q.append(p)
 for s in L['stars']:assert near(s['x'],s['y']) in seen,s['name']+' sealed'
 assert near(L['exitX'],L['exitY']) in seen
 # Both approaches have independent openings on opposite sides of the bay.
 for x in [-3.4,4.1]:assert near(x,.1) in seen,'exit approach blocked'
 print('  Free-space connectivity:',len(seen),'ball-center samples')

def test_scene_references_and_appearance():
 s=(A/'Scenes/Level3.unity').read_text();s1=(A/'Scenes/Level0.unity').read_text()
 records={int(m[1]):m[2] for m in re.finditer(r'^--- !u!\d+ &(\d+)\n([\s\S]*?)(?=^---|\Z)',s,re.M)}
 records1={int(m[1]):m[2] for m in re.finditer(r'^--- !u!\d+ &(\d+)\n([\s\S]*?)(?=^---|\Z)',s1,re.M)}
 assert len(records)==len(re.findall(r'^---',s,re.M)),'duplicate scene IDs'
 for match in re.finditer(r'^--- !u!(\d+) &\d+\n(\w+):',s,re.M):
  typ,name=match.groups();expected={'1':'GameObject','4':'Transform','212':'SpriteRenderer','61':'BoxCollider2D','58':'CircleCollider2D','50':'Rigidbody2D','114':'MonoBehaviour','251':'SurfaceEffector2D'}
  if typ in expected:assert name==expected[typ],(typ,name)
 for id,body in records.items():
  for line in body.splitlines():
   if 'guid:' in line:continue
   for ref in map(int,re.findall(r'\{fileID: (\d+)\}',line)):
    if ref:assert ref in records,(id,'dangling reference',ref)
 # The player renderer keeps its stable ID. Vision Light component IDs differ
 # between historical scene saves, so compare its serialized components after
 # normalizing only their owning GameObject reference.
 assert records[1177520541]==records1[1177520541],'Level 0 player appearance changed'
 def named_components(rs,name):
  go=next(body for body in rs.values() if body.startswith('GameObject:') and f'  m_Name: {name}\n' in body)
  ids=list(map(int,re.findall(r'- component: \{fileID: (\d+)\}',go)))
  return [re.sub(r'm_GameObject: \{fileID: \d+\}', 'm_GameObject: {fileID: 0}', rs[i]) for i in ids]
 assert named_components(records,'Vision Light')==named_components(records1,'Vision Light'),'Level 0 vision light changed'
 assert 'Assembly-CSharp::Level3SceneSetup' in s
 assert 'm_Name: Sector 5 / gap landing' in s
 assert len(re.findall(r'm_Name: Sector [1-5] / (?:conveyor control|lift transfer|inverted dogleg|offset jump|gap landing)\n',s))==5
 # Native UI shows progress: no duplicate IMGUI overlay.
 assert 'Assembly-CSharp::Level3StarHud' not in s
 assert re.search(r'  starsText: \{fileID: [1-9]\d*\}',s)
 assert '  m_Name: STARS\n' in s
 setup=(A/'Scripts/Level3SceneSetup.cs').read_text()
 assert 'ballRenderer.color' not in setup and 'CircleSprite' not in setup
 assert 'light.pointLightOuterRadius = 2f;' in setup
 assert '1f / scale' in setup
 assert (A/'Art/Star.png').read_bytes()==(A/'Resources/Level3Star.png').read_bytes()

def test_guid_uniqueness():
 guids=[]
 for p in A.rglob('*.meta'):
  m=re.search(r'^guid: ([a-f0-9]{32})$',p.read_text(),re.M)
  if m:guids.append(m[1])
 assert len(guids)==len(set(guids))
 for src in A.rglob('*.cs'):
  # Simple lexical balance; not advertised as a C# compile.
  text=src.read_text();text=re.sub(r'//[^\n]*|/\*[\s\S]*?\*/|"(?:\\.|[^"\\])*"','',text)
  stack=[]
  for c in text:
   if c in '{([':stack.append(c)
   if c in '})]':assert stack and stack.pop()=={'}':'{',')':'(',']':'['}[c],src
  assert not stack,src

def test_shared_gameplay_standard():
 movement=(A/'Scripts/PlayerMovement.cs').read_text()
 assert 'groundAcceleration = 38f' in movement
 assert 'airAcceleration = 12f' in movement
 assert 'flatGroundBraking = 13f' in movement
 assert 'class Level3BallMotor : PlayerMovement' in (A/'Scripts/Level3BallMotor.cs').read_text()
 for level in range(4):
  scene=(A/f'Scenes/Level{level}.unity').read_text()
  for setting in ('followSize: 4','revealSize: 8','revealZoomSeconds: 0.25',
                  'moveSpeed: 7.5','groundAcceleration: 38',
                  'airAcceleration: 12','flatGroundBraking: 13','jumpSpeed: 13.2',
                  'maxFallSpeed: 22','coyoteSeconds: 0.1',
                  'jumpBufferSeconds: 0.12','minimumGroundNormal: 0.45'):
   assert setting in scene,(level,setting)
 manager=(A/'Scripts/GameManager.cs').read_text()
 assert 'lightRadiusGrowthPerStar = 0.1f' in manager
 assert '1f + starsCollected * lightRadiusGrowthPerStar' in manager
 assert 'chargesRemaining++;' in manager
 assert 'const string reward = "  \\u00B7  +1 light charge";' in manager
 assert 'if (IsLevel3)\n        {\n            chargesRemaining++;' not in manager
 assert 'cameraFollow.SetRevealView(true)' in manager
 assert 'cameraFollow.SetRevealView(false)' in manager


def test_level0_preserved():
 expected=json.loads((ROOT/'Tools/protected_base_hashes.json').read_text())
 for name,digest in expected.items():
  assert hashlib.sha256((ROOT/name).read_bytes()).hexdigest()==digest,name+' changed'

def test_first_lift_boarding_bound():
 # Initial west-lift ascent: at the jump apex its top must be below the
 # ball's feet. This only checks the initial height budget, not a playthrough.
 lift=next(l for l in L['lifts'] if l['name']=='West ring lift')
 apex_time=13.2/29.43
 lift_top=lift['y']+lift['range']*math.sin(2*math.pi*apex_time/lift['period']+lift['phase'])+lift['h']/2
 floor_top=-11.55+.9/2
 jump_height=13.2**2/(2*29.43)
 assert lift_top-floor_top<jump_height,(lift_top-floor_top,jump_height)

if __name__=='__main__':
 for name,fn in sorted(list(globals().items())):
  if name.startswith('test_') and callable(fn):fn();print('PASS',name)
 print('Non-Unity checks passed. No claim of Editor compilation or full playthrough.')
