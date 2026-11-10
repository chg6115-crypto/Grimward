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
 sk("slash","베기",[1,2],"enemy",[1,2],damaging=True,levels=lv([0,2,4,6,8],[0]*5,[0,.01,.02,.03,.04]),desc="기본기"),
 sk("smite","강타",[1],"enemy",[1],damaging=True,levels=lv([-10,-8,-6,-4,-2],[.4,.45,.5,.55,.6],[.05,.05,.05,.06,.06]),desc="1번 칸 전용, 적 1번만"),
 sk("sweep","휩쓸기",[1,2],"enemy",[1,2],multi=True,damaging=True,levels=lv([-5,-4,-3,-2,-1],[-.4,-.38,-.35,-.33,-.3],[0]*5),desc="적 1·2번 각각 판정"),
 sk("battle_cry","전투의 함성",ALL,"allAllies",desc="3라운드: 자신 피해 +15% · 아군 명중 +5 · 아군 치명 +5%p",
    effects=[eff("damagePct",.15,3,"self"),eff("accuracy",5,3,"allAllies"),eff("crit",.05,3,"allAllies")]),
 # 마법사
 sk("arcane_bolt","마력탄",[1,2,3],"enemy",[1,2,3],damaging=True,levels=lv([5,7,9,11,13],[0]*5,[0,.01,.02,.03,.04]),desc="기본기"),
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
 sk("blessing","축복",ALL,"ally",desc="3라운드: 피해 +10% · 명중 +5 (불경 상대 2배)",
    effects=[eff("damagePct",.1,3,"target","unholy"),eff("accuracy",5,3,"target","unholy")]),
 # 몬스터
 sk("stab","찌르기",[1,2],"enemy",[1,2],damaging=True,levels=one()),
 sk("throw_rock","돌 던지기",[2,3],"enemy",[1,2],damaging=True,levels=one(0,-.3,0)),
 sk("shield_bash","방패 밀치기",[1],"enemy",[1],damaging=True,levels=one(0,-.2,0),desc="명중 시 뒤로 1칸",
    displacement={"ranks":1,"chance":1.0}),
 sk("shield_block","방패 막기",ALL,"self",desc="받는 피해 −30% (1라운드)",effects=[eff("damageTaken",-.3,1,"self")]),
 sk("sling","돌팔매",[3,4],"enemy",[1,2,3],damaging=True,levels=one()),
 sk("volley","연발 투석",[3,4],"enemy",[1,2],multi=True,damaging=True,levels=one(0,-.4,0)),
 sk("blood_rite","피의 주문",ALL,"ally",desc="고블린 HP +4",heal={"min":4,"max":4,"intelligenceDivisor":0}),
 sk("hex_dart","저주 침",[3,4],"enemy",[1,2,3],damaging=True,levels=one(0,-.5,0),desc="명중 시 명중 −5 (2라운드)",
    effects=[eff("accuracy",-5,2)]),
 sk("dark_veil","어둠 장막",ALL,"ally",desc="회피 +10 (2라운드)",effects=[eff("dodge",10,2)]),
 sk("cleave","내려찍기",[1,2],"enemy",[1],damaging=True,levels=one(0,.3,0)),
 sk("war_cry","전쟁 고함",ALL,"allAllies",desc="고블린 전체 피해 +15% (2라운드)",effects=[eff("damagePct",.15,2,"allAllies")]),
 sk("swing","휘두르기",[1,2],"enemy",[1,2],multi=True,damaging=True,levels=one(0,-.3,0),desc="명중 시 뒤로 2칸",
    displacement={"ranks":2,"chance":1.0}),
 sk("summon","부하 소환",ALL,"allySlot",desc="빈 칸에 고블린 1마리(혼자면 2마리), 쿨타임 2",
    summon={"monsterId":"goblin","count":1,"countWhenAlone":2},cooldown=2),
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
 mon("goblin","고블린",10,2,4,10,10,4,0,.05,.2,["normal"],["stab","throw_rock"],"goblin"),
 mon("goblin_shield","고블린 방패병",14,2,3,8,5,3,.15,.05,.6,["normal"],["shield_bash","shield_block"],"goblin"),
 mon("goblin_chief","고블린 우두머리",30,4,7,12,8,5,.05,.05,.5,["normal"],["cleave","war_cry","swing","summon"],"goblin"),
 mon("goblin_slinger","고블린 투석병",8,2,4,12,12,5,0,.05,.1,["normal"],["sling","volley"],"goblin"),
 mon("goblin_shaman","고블린 주술사",8,1,3,10,10,6,0,.05,.1,["normal"],["blood_rite","hex_dart","dark_veil"],"goblin"),
 mon("demon","악마",45,6,10,18,12,7,.075,.05,.5,["unholy"],["claw","stomp"],"demon"),
]
def slot(cands,chance=1.0): return {"candidates":cands,"chance":chance}
back=["goblin","goblin_slinger","goblin_shaman"]
encounters=[
 {"id":"tutorial_battle","name":"고블린 (튜토리얼)","slots":[slot(["goblin"]),slot(["goblin"],.5)]},
 {"id":"tutorial_herb","name":"약초밭 고블린","slots":[slot(["goblin"]),slot(["goblin"]),slot(["goblin"])]},
 {"id":"forest_normal","name":"얕은 숲 고블린 무리","slots":[slot(["goblin"]),slot(["goblin"]),slot(back),slot(back,.5)]},
 {"id":"forest_boss","name":"고블린 우두머리 (보스방)","slots":[slot(["goblin_shield"]),slot(["goblin_chief"]),slot(["goblin_slinger"]),slot(["goblin_shaman"])]},
 {"id":"demon_raid","name":"악마 습격","slots":[slot(["demon"])]},
]
items=[
 {"id":"potion","name":"회복약","heal":10,"curePoison":False,"stack":5,"price":25},
 {"id":"herb","name":"약초","heal":2,"curePoison":True,"stack":5,"price":15},
 {"id":"key","name":"열쇠","heal":0,"curePoison":False,"stack":5,"price":20},
 {"id":"torch","name":"횃불","heal":0,"curePoison":False,"stack":5,"price":10},
]
balance=dict(dayStartMinute=360,nightStartMinute=1200,wakeUpMinute=360,hpBase=16,hpPerVitality=3,attackStrengthDivisor=2,
 accuracyPerDexterity=2,accuracyPerLevel=2,dodgePerDexterity=3,dodgePerceptionDivisor=2,speedStatDivisor=3,critBase=.03,
 critPerDexterity=.005,baseHitChance=.85,hitChanceMin=.05,hitChanceMax=.95,critMin=.05,critMultiplier=1.5,damageReductionCap=.8,
 nightPartyAccuracyPenalty=.1,nightEnemyAccuracyBonus=.1,speedRollDie=8,ranksPerSide=4,itemsPerTurn=1,fleeHpDamageRatio=.2,
 aiBuffChance=.3,aiHealThreshold=.5,defaultPerception=3)
data={"balance":balance,"classes":classes,"skills":skills,"monsters":monsters,"encounters":encounters,"items":items}
out='/home/claude/gw/Assets/_Project/Data/Resources/GameData/gamedata.json'
json.dump(data,open(out,'w',encoding='utf-8'),ensure_ascii=False,indent=1)
print(len(skills),'skills ok')
