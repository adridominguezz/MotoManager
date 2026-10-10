#!/usr/bin/env python3
"""Builds riders/*.json and contracts/*.json from compact rider profiles.

Each profile: level (overall-ish 0-100), trait modifiers, styles, personality hints.
Attributes are ESTIMATES for gameplay, not official data. Edit the profiles here
(or the generated JSON directly) to rebalance riders.
"""
import json, os, hashlib

ROOT = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'MotoManager', 'Resources', 'Data')
ATTRS = ["RaceSkill","QualifyingSkill","Consistency","WetSkill","Overtaking","Defending","TireManagement","Braking",
         "Cornering","ThrottleControl","Adaptability","Fitness","Experience","Aggression","RiskManagement","Mentality",
         "TechnicalFeedback","Starts"]
SHORT = {"race":"RaceSkill","quali":"QualifyingSkill","cons":"Consistency","wet":"WetSkill","ovt":"Overtaking","def":"Defending",
         "tire":"TireManagement","brake":"Braking","corner":"Cornering","throttle":"ThrottleControl","adapt":"Adaptability",
         "fit":"Fitness","exp":"Experience","aggr":"Aggression","risk":"RiskManagement","ment":"Mentality","tech":"TechnicalFeedback","start":"Starts"}

def jitter(key, attr):
    h = int(hashlib.md5((key + attr).encode()).hexdigest()[:6], 16)
    return (h % 5) - 2  # -2..+2, stable

def attributes(rid, level, mods, exp):
    a = {}
    for name in ATTRS:
        a[name] = level + jitter(rid, name)
    a["Experience"] = exp
    a["Aggression"] = 55 + jitter(rid, "aggr") * 3
    for k, v in mods.items():
        a[SHORT[k]] = a.get(SHORT[k], level) + v if k not in ("aggr", "exp") else v
    return {k: max(1, min(99, round(v))) for k, v in a.items()}

def rider(rid, first, last, num, dob, nat, team, level, pot, rep, mods=None, styles=(), exp=60, peak=28,
          gp=70, amb=60, loy=50, temp=50, mot=80, hist="", conf="approximate", end=2026, salary=0, factory=False, man=None):
    return dict(id=rid, firstName=first, lastName=last, number=num, dateOfBirth=dob, nationality=nat, teamId=team,
                attributes=attributes(rid, level, mods or {}, exp), potential=pot, peakAge=peak, reputation=rep, morale=65,
                styles=list(styles), personality=dict(ambition=amb, loyalty=loy, temperament=temp, motivation=mot, grandPrixPreference=gp),
                realHistory=hist, dataConfidence=conf), dict(riderId=rid, teamId=team, manufacturerId=man, startSeason=2025, endSeason=end,
                salaryPerSeason=salary, factoryBacked=factory, dataConfidence="estimated")

LB, SM, AG, TS, CS, SL, WS, QS, RS, DF = ("LateBraker","SmoothRider","AggressiveRider","TireSaver","CornerSpecialist",
    "StraightLineSpecialist","WetSpecialist","QualifyingSpecialist","RaceSpecialist","DefensiveRider")

motogp = [
 rider("marc_marquez","Marc","Márquez",93,"1993-02-17","ES","ducati_lenovo",94,94,99,dict(brake=4,ovt=5,ment=5,aggr=80,risk=-6,wet=3,adapt=4),[LB,AG,RS],exp=99,peak=28,gp=95,amb=95,mot=95,
       hist="7 MotoGP titles (2013, 2014, 2016, 2017, 2018, 2019, 2025), 125cc 2010, Moto2 2012.",conf="verified",salary=12e6,factory=True,man="ducati"),
 rider("bagnaia","Francesco","Bagnaia",63,"1997-01-14","IT","ducati_lenovo",89,91,92,dict(brake=4,quali=2,cons=-3,ment=-3,aggr=50),[LB,QS],exp=88,gp=90,loy=80,
       hist="MotoGP champion 2022 and 2023, Moto2 champion 2018.",conf="verified",salary=7e6,factory=True,man="ducati"),
 rider("alex_marquez","Álex","Márquez",73,"1996-04-23","ES","gresini_motogp",88,88,82,dict(cons=3,tire=2,aggr=55),[SM,RS],exp=84,gp=85,
       hist="Moto3 champion 2014, Moto2 champion 2019, MotoGP runner-up 2025.",conf="verified",salary=2.5e6,man="ducati"),
 rider("aldeguer","Fermín","Aldeguer",54,"2005-04-05","ES","gresini_motogp",85,93,74,dict(brake=4,quali=3,cons=-4,aggr=70,exp=40),[LB,AG],exp=40,peak=27,gp=85,amb=85,
       hist="MotoGP Rookie of the Year 2025, first MotoGP win in Indonesia 2025.",conf="verified",salary=1e6,man="ducati"),
 rider("di_giannantonio","Fabio","Di Giannantonio",49,"1998-10-10","IT","vr46_motogp",85,86,74,dict(cons=1,tech=3),[SM],exp=76,gp=88,
       hist="MotoGP race winner (Qatar 2023).",conf="verified",salary=1.5e6,man="ducati"),
 rider("morbidelli","Franco","Morbidelli",21,"1994-12-04","IT","vr46_motogp",84,84,74,dict(tech=4,cons=1,fit=-2),[SM],exp=88,gp=85,
       hist="Moto2 champion 2017, MotoGP runner-up 2020.",conf="verified",salary=1.5e6,man="ducati"),
 rider("bezzecchi","Marco","Bezzecchi",72,"1998-11-12","IT","aprilia_racing",88,90,85,dict(ovt=4,wet=3,aggr=65,start=3),[AG,RS],exp=78,gp=88,amb=80,
       hist="Multiple MotoGP race winner with Ducati and Aprilia.",conf="verified",salary=3e6,factory=True,man="aprilia"),
 rider("martin","Jorge","Martín",89,"1998-01-29","ES","aprilia_racing",88,91,90,dict(quali=5,start=5,fit=-5,cons=-1,aggr=62),[QS,LB],exp=84,gp=88,amb=90,loy=35,
       hist="MotoGP champion 2024, Moto3 champion 2018. Injury-hit 2025.",conf="verified",salary=8e6,factory=True,man="aprilia"),
 rider("raul_fernandez","Raúl","Fernández",25,"2000-10-23","ES","trackhouse_motogp",84,87,70,dict(cons=-2,brake=2),[LB],exp=66,gp=82,
       hist="Moto2 race winner, first MotoGP win in Australia 2025.",conf="verified",salary=1e6,man="aprilia"),
 rider("ogura","Ai","Ogura",79,"2001-01-26","JP","trackhouse_motogp",83,87,68,dict(tire=4,cons=3,tech=2,aggr=45),[TS,SM],exp=55,gp=85,loy=70,
       hist="Moto2 champion 2024.",conf="verified",salary=0.8e6,man="aprilia"),
 rider("acosta","Pedro","Acosta",37,"2004-05-25","ES","ktm_factory",89,96,88,dict(ovt=5,adapt=5,brake=2,aggr=72,cons=-2),[AG,RS],exp=55,peak=27,gp=92,amb=95,
       hist="Moto3 champion 2021 (rookie), Moto2 champion 2023, multiple MotoGP podiums.",conf="verified",salary=3e6,factory=True,man="ktm"),
 rider("binder","Brad","Binder",33,"1995-08-11","ZA","ktm_factory",85,85,80,dict(ovt=5,start=5,wet=2,aggr=65,quali=-3),[AG,RS],exp=90,gp=85,loy=85,
       hist="Moto3 champion 2016, multiple MotoGP race winner.",conf="verified",salary=3e6,factory=True,man="ktm"),
 rider("bastianini","Enea","Bastianini",23,"1997-12-30","IT","tech3_motogp",85,86,78,dict(tire=6,race=2,quali=-3),[TS,RS],exp=82,gp=85,mot=70,
       hist="Moto2 champion 2020, MotoGP race winner.",conf="verified",salary=2e6,man="ktm"),
 rider("vinales","Maverick","Viñales",12,"1995-01-12","ES","tech3_motogp",85,85,82,dict(quali=4,cons=-4,start=-3,ment=-3),[QS,SM],exp=95,gp=88,
       hist="Moto3 champion 2013, winner with Suzuki, Yamaha and Aprilia.",conf="verified",salary=2.5e6,man="ktm"),
 rider("quartararo","Fabio","Quartararo",20,"1999-04-20","FR","yamaha_factory",88,90,90,dict(quali=5,corner=5,tech=2,aggr=55),[QS,CS,SM],exp=86,gp=90,loy=70,
       hist="MotoGP champion 2021.",conf="verified",salary=12e6,factory=True,man="yamaha"),
 rider("rins","Álex","Rins",42,"1995-12-08","ES","yamaha_factory",82,82,74,dict(tire=4,cons=-2,fit=-3),[TS,SM],exp=92,gp=85,
       hist="MotoGP race winner with Suzuki and Honda.",conf="verified",salary=3e6,factory=True,man="yamaha"),
 rider("miller","Jack","Miller",43,"1995-01-18","AU","pramac_motogp",82,82,78,dict(wet=6,start=4,aggr=70,cons=-3),[AG,WS],exp=94,gp=80,
       hist="MotoGP race winner, wet-weather specialist.",conf="verified",salary=1.5e6,man="yamaha"),
 rider("razgatlioglu","Toprak","Razgatlıoğlu",7,"1996-10-16","TR","pramac_motogp",86,88,88,dict(brake=7,ovt=6,exp=30,adapt=-2,aggr=72),[LB,AG],exp=30,gp=60,amb=90,
       hist="WorldSBK champion 2021, 2024 and 2025. MotoGP rookie in 2026.",conf="verified",salary=2e6,man="yamaha"),
 rider("mir","Joan","Mir",36,"1997-09-01","ES","honda_hrc",85,85,82,dict(cons=2,tire=3,start=3),[SM,TS],exp=86,gp=85,loy=60,
       hist="MotoGP champion 2020, Moto3 champion 2017.",conf="verified",salary=4e6,factory=True,man="honda"),
 rider("marini","Luca","Marini",10,"1997-08-10","IT","honda_hrc",82,83,72,dict(tech=8,cons=3,quali=-3,aggr=40),[SM],exp=78,gp=85,
       hist="Moto2 race winner, Honda development rider.",conf="verified",salary=1.5e6,factory=True,man="honda"),
 rider("zarco","Johann","Zarco",5,"1990-07-16","FR","lcr_motogp",84,84,80,dict(wet=6,tech=4,cons=2,fit=2),[SM,WS],exp=98,peak=30,gp=85,mot=85,
       hist="Moto2 champion 2015 and 2016, MotoGP winner including Le Mans 2025 in the wet.",conf="verified",salary=1.5e6,man="honda"),
 rider("moreira","Diogo","Moreira",11,"2004-05-23","BR","lcr_motogp",80,89,66,dict(exp=35,adapt=3,aggr=60),[RS],exp=35,peak=27,gp=85,amb=85,
       hist="Moto2 champion 2025, first Brazilian in MotoGP since 2007.",conf="approximate (birth date)",salary=0.7e6,man="honda"),
]

worldsbk = [
 rider("bulega","Nicolò","Bulega",11,"1999-10-16","IT","aruba_ducati",80,84,76,dict(cons=3,tire=3,quali=3),[SM,QS],exp=75,gp=55,amb=80,hist="WorldSSP champion 2023, WorldSBK runner-up 2024 and 2025.",salary=1.2e6,factory=True,man="ducati"),
 rider("lecuona","Iker","Lecuona",7,"2000-03-06","ES","aruba_ducati",77,79,64,dict(brake=2,cons=-2),[LB],exp=70,gp=55,hist="Ex-MotoGP, WorldSBK podium finisher.",salary=0.6e6,factory=True,man="ducati"),
 rider("bautista","Álvaro","Bautista",19,"1984-11-21","ES","barni_sbk",78,78,82,dict(fit=-6,start=6,exp=99,tire=3),[SL,SM],exp=99,peak=31,gp=40,mot=70,hist="125cc champion 2006, WorldSBK champion 2022 and 2023.",salary=0.6e6,man="ducati"),
 rider("montella","Yari","Montella",5,"2000-03-23","IT","barni_sbk",75,78,52,dict(),[RS],exp=55,gp=45,hist="WorldSSP race winner.",salary=0.25e6,man="ducati"),
 rider("sam_lowes","Sam","Lowes",14,"1990-09-14","GB","marcvds_sbk",76,76,62,dict(quali=3,fit=-3),[QS],exp=92,peak=29,gp=40,hist="WorldSSP champion 2013, Moto2 race winner.",salary=0.35e6,man="ducati"),
 rider("iannone","Andrea","Iannone",29,"1989-08-09","IT","goeleven_sbk",76,76,70,dict(aggr=72,brake=4,fit=-3),[LB,AG],exp=95,peak=29,gp=60,hist="MotoGP race winner, returned to racing in WorldSBK in 2024.",salary=0.3e6,man="ducati"),
 rider("petrucci","Danilo","Petrucci",9,"1990-10-24","IT","bmw_sbk",77,77,72,dict(wet=6,fit=2,aggr=60),[WS,AG],exp=96,peak=30,gp=45,mot=85,hist="MotoGP race winner, Dakar stage winner.",salary=0.7e6,factory=True,man="bmw"),
 rider("oliveira","Miguel","Oliveira",88,"1995-01-04","PT","bmw_sbk",79,80,76,dict(wet=4,tech=4,fit=-3),[SM,WS],exp=92,gp=60,hist="Five-time MotoGP race winner, moves to WorldSBK in 2026.",salary=1e6,factory=True,man="bmw"),
 rider("van_der_mark","Michael","van der Mark",60,"1992-10-26","NL","bonovo_sbk",74,74,60,dict(cons=2,fit=-3),[SM],exp=95,peak=29,gp=30,hist="WorldSSP champion 2014, Suzuka 8 Hours winner.",salary=0.3e6,man="bmw"),
 rider("locatelli","Andrea","Locatelli",55,"1996-10-16","IT","yamaha_sbk",78,79,68,dict(cons=5,tire=3),[SM,TS],exp=85,gp=35,loy=80,hist="WorldSSP champion 2020, regular WorldSBK podium finisher.",salary=0.6e6,factory=True,man="yamaha"),
 rider("vierge","Xavi","Vierge",97,"1997-04-23","ES","yamaha_sbk",75,76,58,dict(),[SM],exp=78,gp=40,hist="Moto2 podium finisher, ex-Honda HRC WorldSBK rider.",salary=0.4e6,factory=True,man="yamaha",conf="approximate (2026 team)"),
 rider("gardner","Remy","Gardner",87,"1998-02-24","AU","grt_sbk",76,78,62,dict(cons=2),[SM],exp=72,gp=50,hist="Moto2 champion 2021.",salary=0.4e6,man="yamaha"),
 rider("aegerter","Dominique","Aegerter",77,"1990-09-30","CH","grt_sbk",73,73,62,dict(fit=-2,exp=95),[SM],exp=95,peak=29,gp=35,hist="Two-time WorldSSP champion (2021, 2022), MotoE champion.",salary=0.3e6,man="yamaha",conf="approximate (2026 team)"),
 rider("dixon","Jake","Dixon",96,"1996-01-15","GB","honda_sbk",76,77,64,dict(aggr=65,ovt=3),[AG],exp=78,gp=45,hist="Moto2 race winner, joins Honda HRC in WorldSBK for 2026.",salary=0.5e6,factory=True,man="honda",conf="approximate (2026 team)"),
 rider("chantra","Somkiat","Chantra",35,"1998-12-15","TH","honda_sbk",73,75,56,dict(),[RS],exp=60,gp=55,hist="Moto2 race winner, ex-LCR Honda MotoGP rider.",salary=0.4e6,factory=True,man="honda",conf="approximate (2026 team)"),
 rider("alex_lowes","Alex","Lowes",22,"1990-09-14","GB","bimota_sbk",78,78,70,dict(cons=3,tech=4),[SM],exp=96,peak=30,gp=25,loy=75,hist="British Superbike champion 2013, long-time WorldSBK podium finisher.",salary=0.5e6,factory=True,man="bimota"),
 rider("bassani","Axel","Bassani",47,"1999-07-24","IT","bimota_sbk",76,78,60,dict(aggr=62),[AG],exp=70,gp=35,hist="Leading independent rider in WorldSBK.",salary=0.35e6,factory=True,man="bimota"),
 rider("gerloff","Garrett","Gerloff",31,"1995-08-01","US","puccetti_sbk",74,74,58,dict(quali=4),[QS],exp=80,gp=40,hist="WorldSBK podium finisher, MotoGP replacement rider.",salary=0.3e6,man="kawasaki"),
]

moto2 = [
 rider("manuel_gonzalez","Manuel","González",18,"2002-11-05","ES","intact_moto2",73,84,60,dict(cons=3),[RS],exp=60,gp=88,hist="Moto2 race winner and 2025 title contender.",conf="approximate (birth date, 2026 team)",salary=0.3e6),
 rider("agius","Senna","Agius",81,"2005-08-26","AU","intact_moto2",70,84,48,dict(aggr=62),[AG],exp=40,gp=85,conf="approximate",salary=0.15e6),
 rider("canet","Arón","Canet",44,"1999-09-30","ES","fantic_moto2",73,76,58,dict(quali=3,cons=-3),[QS],exp=70,gp=80,hist="Multiple Moto2 runner-up finishes, first win 2025.",conf="approximate",salary=0.3e6),
 rider("baltus","Barry","Baltus",7,"2004-07-01","BE","fantic_moto2",71,81,50,dict(),[RS],exp=50,gp=85,hist="Moto2 race winner 2025.",conf="approximate (birth date)",salary=0.2e6),
 rider("arbolino","Tony","Arbolino",14,"2000-08-01","IT","pramac_moto2",71,75,56,dict(cons=2),[SM],exp=70,gp=80,hist="Moto2 runner-up 2023.",conf="approximate",salary=0.25e6),
 rider("vietti","Celestino","Vietti",13,"2001-10-13","IT","speedrs_moto2",72,77,56,dict(tire=3),[TS],exp=68,gp=82,hist="Moto2 race winner.",conf="approximate",salary=0.25e6),
 rider("alonso","David","Alonso",80,"2006-04-25","CO","aspar_moto2",72,90,64,dict(ovt=5,aggr=70,cons=-2),[AG,RS],exp=45,peak=27,gp=92,amb=90,hist="Moto3 champion 2024 with a record 14 wins.",conf="approximate (birth date)",salary=0.3e6),
 rider("holgado","Daniel","Holgado",96,"2005-01-14","ES","aspar_moto2",70,82,52,dict(),[RS],exp=45,gp=88,hist="Moto3 race winner.",conf="approximate",salary=0.15e6),
 rider("rueda","José Antonio","Rueda",99,"2006-02-03","ES","ajo_moto2",71,88,60,dict(ovt=4,aggr=65),[AG],exp=40,peak=27,gp=90,amb=85,hist="Moto3 champion 2025.",conf="approximate (birth date)",salary=0.2e6),
 rider("veijer","Collin","Veijer",95,"2005-05-02","NL","ajo_moto2",69,84,50,dict(),[RS],exp=40,gp=85,hist="Moto3 race winner.",conf="approximate (birth date, 2026 team)",salary=0.15e6),
 rider("guevara","Izan","Guevara",28,"2004-06-23","ES","pramac_moto2",69,80,50,dict(),[SM],exp=50,gp=85,hist="Moto3 champion 2022.",conf="approximate",salary=0.15e6),
 rider("roberts","Joe","Roberts",16,"1997-06-26","US","american_moto2",70,72,52,dict(cons=2),[SM],exp=72,gp=80,hist="Moto2 race winner.",conf="approximate",salary=0.2e6),
 rider("sasaki","Ayumu","Sasaki",71,"2000-11-02","JP","rw_moto2",68,73,46,dict(),[SM],exp=62,gp=80,conf="approximate",salary=0.12e6),
 rider("salac","Filip","Salač",12,"2001-11-27","CZ","marcvds_moto2",68,73,46,dict(),[SM],exp=60,gp=80,conf="approximate",salary=0.12e6),
 rider("lopez","Alonso","López",21,"2002-01-02","ES","speedrs_moto2",70,76,52,dict(aggr=62),[AG],exp=60,gp=80,hist="Moto2 race winner.",conf="approximate",salary=0.15e6),
 rider("sergio_garcia","Sergio","García",3,"2003-02-05","ES","pons_moto2",70,78,52,dict(),[RS],exp=58,gp=82,hist="Moto2 race winner.",conf="approximate",salary=0.15e6),
 rider("oncu","Deniz","Öncü",53,"2003-07-26","TR","msi_moto2",68,76,46,dict(aggr=60),[AG],exp=55,gp=78,conf="approximate",salary=0.12e6),
 rider("arenas","Albert","Arenas",75,"1996-09-11","ES","italtrans_moto2",68,69,48,dict(),[RS],exp=80,peak=28,gp=75,hist="Moto3 champion 2020.",conf="approximate",salary=0.12e6),
]

moto3 = [
 rider("quiles","Máximo","Quiles",28,"2007-12-01","ES","aspar_moto3",64,88,44,dict(aggr=62),[AG],exp=30,peak=27,gp=92,amb=85,hist="Moto3 race winner in his rookie season (2025).",conf="approximate (birth date)",salary=0.06e6),
 rider("carpe","Álvaro","Carpe",83,"2007-07-21","ES","ajo_moto3",63,84,40,dict(),[RS],exp=30,gp=90,conf="approximate",salary=0.05e6),
 rider("kelso","Joel","Kelso",66,"2003-03-02","AU","boe_moto3",63,70,40,dict(ovt=3),[RS],exp=55,gp=80,conf="approximate",salary=0.05e6),
 rider("adrian_fernandez","Adrián","Fernández",31,"2004-09-14","ES","leopard_moto3",63,74,40,dict(),[RS],exp=50,gp=85,conf="approximate",salary=0.05e6),
 rider("furusato","Taiyo","Furusato",72,"2005-05-23","JP","asia_moto3",63,76,40,dict(cons=2),[SM],exp=48,gp=85,hist="Moto3 race winner.",conf="approximate",salary=0.05e6),
 rider("yamanaka","Ryusei","Yamanaka",6,"2001-11-18","JP","msi_moto3",61,66,36,dict(),[SM],exp=62,gp=75,conf="approximate",salary=0.04e6),
 rider("nepa","Stefano","Nepa",82,"2001-12-20","IT","sic58_moto3",61,66,36,dict(),[SM],exp=62,gp=75,conf="approximate",salary=0.04e6),
 rider("lunetta","Luca","Lunetta",58,"2006-12-28","IT","sic58_moto3",62,78,38,dict(),[RS],exp=40,gp=85,conf="approximate",salary=0.04e6),
 rider("bertelle","Matteo","Bertelle",18,"2004-06-24","IT","levelup_moto3",62,70,38,dict(aggr=65),[AG],exp=52,gp=80,conf="approximate",salary=0.04e6),
 rider("pini","Guido","Pini",94,"2008-01-11","IT","intact_moto3",61,82,36,dict(),[RS],exp=30,gp=85,conf="approximate",salary=0.04e6),
 rider("ogden","Scott","Ogden",19,"2003-12-04","GB","gryd_moto3",61,68,36,dict(),[SM],exp=50,gp=78,conf="approximate",salary=0.04e6),
 rider("esteban","Joel","Esteban",78,"2005-05-03","ES","cip_moto3",61,72,36,dict(),[RS],exp=45,gp=80,conf="approximate",salary=0.04e6),
]

worldssp = [
 rider("manzi","Stefano","Manzi",62,"1999-03-12","IT","tenkate_ssp",70,72,52,dict(aggr=65),[AG],exp=70,gp=45,hist="WorldSSP race winner and title contender.",conf="approximate",salary=0.15e6),
 rider("masia","Jaume","Masià",5,"2000-11-22","ES","orelac_ssp",69,72,50,dict(),[RS],exp=65,gp=50,hist="Moto3 race winner, WorldSSP podium finisher.",conf="approximate",salary=0.12e6),
 rider("debise","Valentin","Debise",53,"1992-02-19","FR","gmt94_ssp",67,67,44,dict(exp=88),[SM],exp=88,peak=28,gp=30,conf="approximate",salary=0.08e6),
 rider("booth_amos","Tom","Booth-Amos",12,"1996-04-18","GB","ptr_ssp",67,69,44,dict(),[SM],exp=65,gp=30,conf="approximate",salary=0.08e6),
 rider("mahias","Lucas","Mahias",25,"1989-01-14","FR","kawasaki_ssp",66,66,46,dict(exp=92),[SM],exp=92,peak=28,gp=30,hist="WorldSSP champion 2017.",conf="approximate",salary=0.08e6),
 rider("caricasulo","Federico","Caricasulo",64,"1996-02-14","IT","mv_ssp",67,69,46,dict(),[SM],exp=72,gp=40,conf="approximate",salary=0.09e6),
]

all_groups = dict(motogp=motogp, worldsbk=worldsbk, moto2=moto2, moto3=moto3, worldssp=worldssp)
# Contract lengths: most MotoGP deals end with the 1000cc era (2026); a few already run into 2027.
LONGER = {"moreira", "razgatlioglu", "aldeguer"}
contracts = []
for cat, items in all_groups.items():
    for r, c in items:
        if cat == "motogp":
            c["endSeason"] = 2027 if r["id"] in LONGER else 2026
        else:
            c["endSeason"] = 2026 + int(hashlib.md5(r["id"].encode()).hexdigest()[:2], 16) % 2
    riders = [r for r, _ in items]
    with open(os.path.join(ROOT, 'riders', cat + '.json'), 'w') as f:
        json.dump(riders, f, indent=2, ensure_ascii=False); f.write('\n')
    for _, c in items:
        if c['manufacturerId'] is None:
            c.pop('manufacturerId')
        contracts.append(c)
with open(os.path.join(ROOT, 'contracts', 'contracts_2026.json'), 'w') as f:
    json.dump(contracts, f, indent=2, ensure_ascii=False); f.write('\n')
print({k: len(v) for k, v in all_groups.items()}, 'contracts', len(contracts))
