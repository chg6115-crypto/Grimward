import json
def lv(acc, dmg, crit):
    return [{"accuracy":a,"damage":d,"crit":c} for a,d,c in zip(acc,dmg,crit)]
def one(acc=0,dmg=0.0,crit=0.0): return [{"accuracy":acc,"damage":dmg,"crit":crit}]
def eff(stat,value,duration,target="target",doubled=""):
    return {"kind":"stat","stat":stat,"value":value,"duration":duration,"target":target,"doubledVsType":doubled,"damagePerTurn":0}
def sk(id,name,use,target,tr=None,multi=False,damaging=False,levels=None,desc="",**kw):
    d={"id":id,"name":name,"useRanks":use,"target":target,"targetRanks":tr or [],"multi":multi,
       "damaging":damaging,"ignoreArmor":False,"levels":levels or [],"fixedDamage":{"min":0,"max":0,"intelligenceMultiplier":0},
       "typeBonuses":[],"effects":[],"heal":{"min":0,"max":0,"intelligenceDivisor":0},
       "displacement":{"ranks":0,"chance":0},"summon":{"monsterId":"","count":0,"countWhenAlone":0},
       "curePoison":False,"cooldown":0,"description":desc}
    d.update(kw); return d
ALL=[1,2,3,4]
skills=[
 # 검사
 sk("slash","베기",[1,2],"enemy",[1,2],damaging=True,levels=lv([0,2,4,6,8],[0]*5,[0,.01,.02,.03,.04]),desc="기본기 · 모든 위치에서 모든 위치로"),
 sk("smite","강타",[1],"enemy",[1],damaging=True,levels=lv([-10,-8,-6,-4,-2],[.4,.45,.5,.55,.6],[.05,.05,.05,.06,.06]),desc="1번 칸 전용, 적 1번만"),
 sk("sweep","휩쓸기",[1,2],"enemy",[1,2],multi=True,damaging=True,levels=lv([-5,-4,-3,-2,-1],[-.4,-.38,-.35,-.33,-.3],[0]*5),desc="적 1·2번 각각 판정"),
 sk("battle_cry","전투의 함성",ALL,"allAllies",desc="3라운드: 자신 피해 +15% · 아군 명중 +10 · 아군 치명 +5%p",
    effects=[eff("damagePct",.15,3,"self"),eff("accuracy",5,3,"allAllies"),eff("crit",.05,3,"allAllies")]),
 # 마법사
 sk("arcane_bolt","마력탄",ALL,"enemy",ALL,damaging=True,levels=lv([5,7,9,11,13],[0]*5,[0,.01,.02,.03,.04]),desc="기본기 · 모든 위치에서 모든 위치로"),
 sk("fireball","화염구",[2,3,4],"enemy",[1,2,3],multi=True,damaging=True,levels=lv([-5,-4,-3,-2,-1],[-.35,-.33,-.3,-.28,-.25],[0]*5),desc="적 1·2·3번 각각 판정"),
 sk("ice_lance","얼음 창",[2,3],"enemy",[2,3,4],damaging=True,levels=lv([-5,-4,-3,-2,-1],[.2,.25,.3,.35,.4],[.05,.05,.06,.06,.07]),
    desc="적 후열 전용. 명중 시 속도 −2 (2라운드)",effects=[eff("speed",-2,2)]),
 sk("arcane_ward","마력 보호막",ALL,"ally",desc="2라운드: 받는 피해 −30%",effects=[eff("damageTaken",-.3,2)]),
 # 궁수
 sk("aimed_shot","조준 사격",[3,4],"enemy",ALL,damaging=True,levels=one(5,0,.05),desc="3·4번 칸에서 적 어디든"),
 sk("piercing_shot","관통 사격",[3,4],"enemy",[1,2],multi=True,damaging=True,levels=one(0,-.2,0),desc="적 1·2번 관통"),
 sk("first_aid","응급처치",ALL,"ally",desc="HP +(3 + 지능÷2)",heal={"min":3,"max":3,"intelligenceDivisor":2}),
 sk("magic_arrow","매직 애로우",[2,3,4],"enemy",ALL,damaging=True,ignoreArmor=True,levels=one(10,0,0),
    fixedDamage={"min":2,"max":4,"intelligenceMultiplier":1},desc="피해 (2~4)+지능, 방어도 무시"),
 # 도적
 sk("ambush_stab","급습",[1,2,3],"enemy",[1,2],damaging=True,levels=one(5,0,.1),desc="치명타 특화"),
 sk("poison_blade","독 칼날",[1,2,3],"enemy",[1,2,3],damaging=True,levels=one(0,-.3,0),desc="명중 시 중독 3라운드 (차례 시작마다 2)",
    effects=[{"kind":"poison","stat":"","value":0,"duration":3,"target":"target","doubledVsType":"","damagePerTurn":2}]),
 sk("throwing_knife","투척 단검",[3,4],"enemy",[3,4],damaging=True,levels=one(0,-.1,.05),desc="적 후열(3·4번)"),
 sk("grapple","갈고리 사슬",[1,2,3],"enemy",[2,3,4],damaging=True,levels=one(5,-.5,0),desc="명중 시 앞으로 2칸 끌어당김",
    displacement={"ranks":-2,"chance":1.0}),
 # 사제
 sk("healing_light","치유의 빛",[2,3,4],"ally",desc="HP +5~7",heal={"min":5,"max":7,"intelligenceDivisor":0}),
 sk("prayer","기도",ALL,"allAllies",desc="아군 전체 HP +2~3",heal={"min":2,"max":3,"intelligenceDivisor":0}),
 sk("holy_strike","성스러운 일격",[1,2],"enemy",[1,2],damaging=True,levels=one(0,-.1,0),desc="불경 피해 +50%",
    typeBonuses=[{"type":"unholy","damage":.5}]),
 sk("blessing","축복",ALL,"ally",desc="3라운드: 피해 +10% · 명중 +10 (불경 상대 2배)",
    effects=[eff("damagePct",.1,3,"target","unholy"),eff("accuracy",5,3,"target","unholy")]),
 # 몬스터
 sk("stab","찌르기",[1,2],"enemy",[1,2],damaging=True,levels=one()),
 sk("elite_stab","정예 찌르기",[1,2],"enemy",[1,2],damaging=True,levels=one(),desc="고블린 정예 전용 · 얕은 베임"),
 sk("throw_rock","돌 던지기",[2,3],"enemy",[1,2],damaging=True,levels=one(0,-.3,0)),
 sk("shield_bash","방패 밀치기",[1],"enemy",[1],damaging=True,levels=one(0,-.2,0),desc="명중 시 뒤로 1칸",
    displacement={"ranks":1,"chance":1.0}),
 sk("shield_block","방패 막기",ALL,"self",desc="받는 피해 −30% (1라운드)",effects=[eff("damageTaken",-.3,1,"self")]),
 sk("sling","돌팔매",[3,4],"enemy",[1,2,3],damaging=True,levels=one()),
 sk("volley","연발 투석",[3,4],"enemy",[1,2],multi=True,damaging=True,levels=one(0,-.4,0)),
 sk("blood_rite","피의 주문",ALL,"ally",desc="고블린 HP +4",heal={"min":4,"max":4,"intelligenceDivisor":0}),
 sk("hex_dart","저주 침",[3,4],"enemy",[1,2,3],damaging=True,levels=one(0,-.5,0),desc="명중 시 명중 −10 (2라운드)",
    effects=[eff("accuracy",-5,2)]),
 sk("frenzy","광폭화",[3,4],"ally",desc="뒷줄에서 가장 강한 아군을 광폭화: 피해 +25% · 속도 +3 (2라운드, 누적)",
    effects=[eff("damagePct",.25,2),eff("speed",3,2)],aiUseChance=.8),
 # 홉고블린 (보스: 조직적인 전투, 방패와 무기)
 sk("hob_slash","검 베기",[1,2],"enemy",[1,2],damaging=True,levels=one(5,0,0.05),desc="정확한 일격"),
 sk("hob_shield_bash","방패 강타",[1,2],"enemy",[1],damaging=True,levels=one(0,-.2,0),desc="명중 시 뒤로 1칸",
    displacement={"ranks":1,"chance":1.0}),
 sk("hob_rally","부하 독려",ALL,"allAllies",desc="2라운드: 아군 전체 피해 +20% · 받는 피해 −15% · 방어도 +10% (재사용 대기 2)",
    effects=[eff("damagePct",.2,2),eff("damageTaken",-.15,2),eff("armor",.1,2)],cooldown=2),
 sk("summon","부하 소환",ALL,"allySlot",desc="앞칸에 고블린 정예를 부르고(최대 2마리 유지, 정예가 쓰러져야 다시 소환) 홉고블린·아군은 뒤로 물러남 (진형 붕괴 아님), 쿨타임 2",
    summon={"monsterId":"goblin_elite","count":2,"countWhenAlone":2,"inFront":True,"maxAlive":2},cooldown=2),
 # 버그베어 (보스: 높은 HP, 강력한 타격, 진형 붕괴)
 sk("bug_maul","철퇴 내려치기",[1,2],"enemy",[1],damaging=True,levels=one(-5,.4,.05),desc="강력한 한 방"),
 sk("swing","휘두르기",[1,2],"enemy",[1,2],multi=True,damaging=True,levels=one(0,-.3,0),desc="명중 시 뒤로 2칸",
    displacement={"ranks":2,"chance":1.0}),
 sk("bug_drag","덮쳐 끌기",[1,2],"enemy",[2,3],damaging=True,levels=one(0,-.4,0),desc="명중 시 앞으로 2칸 끌어당김",
    displacement={"ranks":-2,"chance":1.0}),
 sk("bug_roar","위협의 포효",ALL,"enemy",ALL,multi=True,damaging=False,levels=one(0,0,0),desc="명중한 영웅 명중 −10 (2라운드)",
    effects=[eff("accuracy",-5,2)]),
 sk("claw","할퀴기",ALL,"enemy",[1,2],damaging=True,levels=one()),
 sk("stomp","짓밟기",ALL,"enemy",[1,2],multi=True,damaging=True,levels=one(-5,-.3,0)),
]
def cls(id,name,role,s,d,v,i,wmin,wmax,stat,armor,res,skills):
    return dict(id=id,name=name,role=role,strength=s,dexterity=d,vitality=v,intelligence=i,weaponMin=wmin,weaponMax=wmax,
                attackStat=stat,armor=armor,formationResist=res,skills=skills)
classes=[
 cls("swordsman","검사","전방 딜러",5,3,4,1,4,8,"strength",.05,.4,["slash","smite","sweep","battle_cry"]),
 cls("mage","마법사","중후열 광역·디버프",1,4,2,6,2,5,"intelligence",0,.2,["arcane_bolt","fireball","ice_lance","arcane_ward"]),
 cls("archer","궁수","후열 딜러·보조 회복",3,6,3,1,3,6,"strength",0,.2,["aimed_shot","piercing_shot","magic_arrow","first_aid"]),
 cls("rogue","도적","유틸리티·중독",4,6,2,1,3,6,"strength",0,.3,["ambush_stab","poison_blade","throwing_knife","grapple"]),
 cls("priest","사제","힐러·서포터",2,3,3,5,2,5,"strength",0,.3,["healing_light","prayer","holy_strike","blessing"]),
]
def mon(id,name,hp,amin,amax,acc,dod,spd,armor,crit,res,types,skills,family):
    return dict(id=id,name=name,hp=hp,attackMin=amin,attackMax=amax,accuracy=acc,dodge=dod,speed=spd,armor=armor,crit=crit,
                formationResist=res,types=types,skills=skills,family=family)
monsters=[
 mon("goblin","고블린",15,3,5,12,12,6,0,.05,.2,["normal"],["stab","throw_rock"],"goblin"),
 mon("goblin_elite","고블린 정예",22,5,7,12,12,6,0,.05,.2,["normal"],["elite_stab","throw_rock"],"goblin"),
 mon("goblin_shield","고블린 방패병",20,3,4,8,5,5,.15,.05,.6,["normal"],["shield_bash","shield_block"],"goblin"),
 mon("goblin_slinger","고블린 투석병",12,3,5,12,12,7,0,.05,.1,["normal"],["sling","volley"],"goblin"),
 mon("goblin_shaman","고블린 주술사",12,2,4,10,10,7,0,.05,.1,["normal"],["blood_rite","hex_dart","frenzy"],"goblin"),
 mon("hobgoblin","홉고블린",60,5,9,14,8,20,.15,.05,.3,["normal"],["hob_slash","hob_shield_bash","hob_rally","summon"],"goblin"),
 mon("bugbear","버그베어",70,5,9,12,6,5,.05,.05,.7,["normal"],["bug_maul","swing","bug_drag","bug_roar"],"goblin"),
 mon("demon","악마",60,8,12,18,12,8,.075,.05,.5,["unholy"],["claw","stomp"],"demon"),
]
def passive(id,name,kind,desc,effects=None,ranks=None,prot=None):
    return {"id":id,"name":name,"kind":kind,"description":desc,"effects":effects or [],"ranks":ranks or [],"protectedRanks":prot or []}
for m in monsters:
    m["passives"]=[]
    m["size"]=2 if m["id"]=="bugbear" else 1
    m["actionsPerTurn"]=2 if m["id"]=="bugbear" else 1
    m["varianceScale"]=0.5 if m["id"] in ("hobgoblin","bugbear","demon") else 1.0
next(m for m in monsters if m["id"]=="hobgoblin")["passives"]=[passive("commander","지휘관","allyAura",
    "홉고블린이 살아 있는 동안 다른 아군 전체 방어도 +10% (홉고블린이 쓰러지면 풀림)",
    [eff("armor",.1,0,"target")])]
next(m for m in monsters if m["id"]=="goblin_shaman")["passives"]=[passive("hex_blessing","주술의 가호","allyAura",
    "주술사가 살아 있는 동안 다른 아군 전체 피해 +5% (주술사마다 따로 적용, 쓰러지면 풀림)",[eff("damagePct",.05,0,"target")])]
next(m for m in monsters if m["id"]=="bugbear")["passives"]=[passive("bulwark","거구의 벽","guardBackline",
    "버그베어가 1·2번 칸에 있는 동안 아군 3·4번 칸은 위치가 바뀌지 않음",None,[1,2],[3,4])]
def slot(cands,chance=1.0): return {"candidates":cands,"chance":chance}
back=["goblin","goblin_slinger","goblin_shaman"]
encounters=[
 {"id":"tutorial_battle","name":"고블린 (튜토리얼)","variants":[],"slots":[slot(["goblin"]),slot(["goblin"],.5)]},
 {"id":"tutorial_herb","name":"약초밭 고블린","slots":[slot(["goblin"]),slot(["goblin"]),slot(["goblin"])]},
 {"id":"forest_normal","name":"얕은 숲 고블린 무리","slots":[slot(["goblin"]),slot(["goblin"]),slot(back),slot(back,.5)]},
 {"id":"forest_boss","name":"얕은 숲 보스 (무작위)","slots":[],"variants":["forest_boss_hobgoblin","forest_boss_bugbear"]},
 {"id":"forest_boss_hobgoblin","name":"홉고블린 (보스방)","slots":[slot(["hobgoblin"]),slot(["goblin_shaman"])]},
 {"id":"forest_boss_bugbear","name":"버그베어 (보스방)","slots":[slot(["bugbear"]),slot(["goblin_shaman"]),slot(["goblin_shaman"])]},
 {"id":"demon_raid","name":"악마 습격","slots":[slot(["demon"])]},
]
items=[
 {"id":"potion","name":"회복약","heal":10,"curePoison":False,"stack":5,"price":25},
 {"id":"herb","name":"약초","heal":2,"curePoison":True,"stack":5,"price":15},
 {"id":"key","name":"열쇠","heal":0,"curePoison":False,"stack":5,"price":20},
 {"id":"torch","name":"횃불","heal":0,"curePoison":False,"stack":5,"price":10},
]
balance=dict(dayStartMinute=360,nightStartMinute=1200,wakeUpMinute=360,hpBase=15,hpPerVitality=3,attackStrengthDivisor=2,
 accuracyPerDexterity=2,accuracyPerLevel=2,dodgePerDexterity=3,dodgePerceptionDivisor=2,speedStatDivisor=3,critBase=.03,
 critPerDexterity=.005,baseHitChance=.85,hitChanceMin=.05,hitChanceMax=.95,critMin=.05,critMultiplier=1.5,damageReductionCap=.8,minHitDamage=1,injuryMaxPerAdventurer=2,injuryHpThreshold=.3,injuryThresholdChance=.5,
 nightPartyAccuracyPenalty=.1,nightEnemyAccuracyBonus=.1,speedRollDie=8,ranksPerSide=4,itemsPerTurn=1,fleeHpDamageRatio=.2,
 aiBuffChance=.3,aiHealThreshold=.5,defaultPerception=3,bagSlots=12,monsterHpVariance=.15,monsterAccuracyDodgeVariance=2,monsterSpeedVariance=1)
types=[{"id":"unholy","name":"불경"},{"id":"humanoid","name":"인간형"},{"id":"normal","name":"일반"},{"id":"eldritch","name":"이물"}]
KIND={"slash":"근접","smite":"근접","sweep":"근접·다중","battle_cry":"버프","arcane_bolt":"원거리·마법","fireball":"원거리·다중","ice_lance":"원거리·디버프","arcane_ward":"버프","aimed_shot":"원거리","piercing_shot":"원거리·다중","first_aid":"회복","magic_arrow":"원거리·마법","ambush_stab":"근접","poison_blade":"근접·중독","throwing_knife":"원거리","grapple":"원거리·진형 붕괴","healing_light":"회복","prayer":"회복·전체","holy_strike":"근접","blessing":"버프","stab":"근접","elite_stab":"근접","throw_rock":"원거리","shield_bash":"근접·진형 붕괴","shield_block":"버프","sling":"원거리","volley":"원거리·다중","blood_rite":"회복","hex_dart":"원거리·디버프","frenzy":"버프","hob_slash":"근접","hob_shield_bash":"근접·진형 붕괴","hob_rally":"버프·전체","summon":"소환","bug_maul":"근접","swing":"근접·다중·진형 붕괴","bug_drag":"근접·진형 붕괴","bug_roar":"디버프·전체","claw":"근접","stomp":"근접·다중"}
for s_ in skills: s_["kind"]=KIND[s_["id"]]
# ── 11-11 명중 폭 확대: 기술 명중 보정(1레벨 기준 새 값, 레벨 배열은 같은 폭으로 이동) · 몬스터 회피 개성 ──
ACC={"slash":5,"smite":-15,"sweep":-10,"arcane_bolt":10,"fireball":-10,"ice_lance":0,"aimed_shot":15,"holy_strike":5,
     "throw_rock":-5,"volley":-10,"hex_dart":5,"hob_slash":10,"bug_maul":-15,"swing":-5,"bug_drag":-5,"claw":5,"stomp":-10}
for s_ in skills:
    if s_["id"] in ACC and s_["levels"]:
        d=ACC[s_["id"]]-s_["levels"][0]["accuracy"]
        for l in s_["levels"]: l["accuracy"]+=d
BUFF={("hex_dart","accuracy"):-10,("bug_roar","accuracy"):-10,("battle_cry","accuracy"):10,("blessing","accuracy"):10}
for s_ in skills:
    for e in s_["effects"]:
        if (s_["id"],e["stat"]) in BUFF: e["value"]=BUFF[(s_["id"],e["stat"])]
DODGE={"goblin":15,"goblin_elite":10,"goblin_slinger":15,"goblin_shaman":10,"hobgoblin":5,"bugbear":0,"demon":20}
for m in monsters:
    if m["id"] in DODGE: m["dodge"]=DODGE[m["id"]]
# ── 11-11 부상 ──
def stage(name,effects=None,bleed=0,rounds=0): return {"name":name,"effects":effects or [],"bleedPerTurn":bleed,"bleedRounds":rounds}
injuries=[
 {"id":"shallow_cut","name":"베임","minor":stage("얕은 베임",bleed=1,rounds=3),"major":stage("깊은 베임",bleed=2,rounds=4),
  "fieldTreatable":True,"healedByRest":False,"description":"출혈: 전투마다 처음 몇 라운드 동안 차례 시작 시 피해. 응급처치·치유의 빛·붕대로 치료"},
 {"id":"concussion","name":"뇌진탕","minor":stage("뇌진탕 (경상)",[eff("accuracy",-5,0)]),"major":stage("뇌진탕 (중상)",[eff("accuracy",-10,0)]),
  "fieldTreatable":False,"healedByRest":True,"description":"명중 감소. 하룻밤 휴식으로 자연 회복"},
]
TAG={"sling":("concussion",.3),"bug_maul":("concussion",.3),"elite_stab":("shallow_cut",.1),"hob_slash":("shallow_cut",.25)}
for s_ in skills:
    s_["injury"]={"injuryId":TAG[s_["id"]][0],"chance":TAG[s_["id"]][1]} if s_["id"] in TAG else {"injuryId":"","chance":0}
    s_["treatInjury"]=s_["id"] in ("first_aid","healing_light")
for it in items: it.setdefault("treatInjury",False); it.setdefault("cureBleed",False)
items.append({"id":"bandage","name":"붕대","heal":0,"curePoison":False,"treatInjury":True,"cureBleed":True,"stack":3,"price":15})
data={"injuries":injuries,"monsterTypes":types,"balance":balance,"classes":classes,"skills":skills,"monsters":monsters,"encounters":encounters,"items":items}
out='/home/claude/gw/Assets/_Project/Data/Resources/GameData/gamedata.json'
json.dump(data,open(out,'w',encoding='utf-8'),ensure_ascii=False,indent=1)
print(len(skills),'skills ok')
