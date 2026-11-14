"""Original deterministic electronic/mechanical cues; no third-party samples."""
from pathlib import Path
import math, random, wave, array, hashlib, json

ROOT = Path(__file__).resolve().parents[1] / 'Assets/ChapterOne/Audio'
RATE = 32000
CUES = {
    'patrol_loop': (2.0, 'motor'), 'ambient_loop': (3.0, 'ambient'),
    'alert': (.55, 'rise'), 'chase_loop': (1.0, 'chase'),
    'pulse': (.45, 'pulse'), 'recover': (.65, 'rise'),
    'charge_loop': (1.0, 'charge'), 'charge_full': (.7, 'chime'),
    'supply': (.8, 'chime'), 'door': (1.0, 'mechanical'),
    'success': (1.2, 'success'), 'failure': (.8, 'fall'), 'ui_click': (.09, 'click')
}

def synth(name, seconds, kind):
    rng = random.Random(7300 + sum(map(ord, name)))
    values = array.array('h'); phase = 0.; filtered_noise = 0.; peak = 0.
    total = round(seconds * RATE)
    for i in range(total):
        t = i / RATE; x = t / seconds
        filtered_noise = .92 * filtered_noise + .08 * rng.uniform(-1, 1)
        envelope = min(1., t / .012, (seconds - t) / .035)
        if kind == 'motor':
            y = .13 * math.sin(2*math.pi*100*t) + .045 * math.sin(2*math.pi*200*t) + .06 * filtered_noise
        elif kind == 'ambient':
            y = .045 * math.sin(2*math.pi*60*t) + .03 * filtered_noise
        elif kind == 'charge':
            y = (.08 * math.sin(2*math.pi*220*t) + .035 * math.sin(2*math.pi*880*t)) * (.8 + .2*math.sin(2*math.pi*2*t))
        elif kind == 'chase':
            y = .14 * math.sin(2*math.pi*330*t) * (.3+.7*max(0, math.sin(2*math.pi*3*t))) + .025 * math.sin(2*math.pi*110*t)
        elif kind in ('rise', 'fall', 'pulse'):
            f = 300 + 400*x if kind == 'rise' else 420-270*x if kind == 'fall' else 1500-1250*x
            phase += 2*math.pi*f/RATE
            y = .22*math.sin(phase)*(1-x)**.6
            if kind == 'pulse': y += .28*filtered_noise*(1-x)**2
        elif kind in ('chime', 'success'):
            notes = (440., 550., 660., 880.) if kind == 'success' else (660., 880., 1100.)
            y = 0.
            for index, frequency in enumerate(notes):
                age = t-index*seconds/(len(notes)+2)
                if age >= 0: y += .12*math.sin(2*math.pi*frequency*age)*math.exp(-6*age)*min(1, age/.008)
        elif kind == 'mechanical':
            y = .3*filtered_noise*math.sin(math.pi*x) + .06*math.sin(2*math.pi*90*t)
        else:
            y = .14*math.sin(2*math.pi*740*t)*math.exp(-40*t)
        y = max(-.8, min(.8, y*max(0, envelope)))
        peak = max(peak, abs(y)); values.append(round(y*32767))
    path = ROOT/(name+'.wav')
    with wave.open(str(path), 'wb') as output:
        output.setnchannels(1); output.setsampwidth(2); output.setframerate(RATE); output.writeframes(values.tobytes())
    return {'file':path.name,'seconds':seconds,'peak':round(peak,4),'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}

if __name__ == '__main__':
    ROOT.mkdir(parents=True, exist_ok=True)
    records = [synth(name, *spec) for name, spec in CUES.items()]
    (ROOT/'SOURCE.json').write_text(json.dumps({'origin':'Original deterministic synthesis for LastCellOfPower; no sampled external audio','generator':'Tools/GenerateChapterAudio.py','sampleRate':RATE,'channels':1,'bits':16,'files':records},indent=2),encoding='utf-8')
    print(json.dumps({'generated':len(records),'peakMaximum':max(x['peak'] for x in records)}))
