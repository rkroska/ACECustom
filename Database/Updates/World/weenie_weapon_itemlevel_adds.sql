##item levels
## max level propertyint 319
## equipment set id 265 (avoid)
## rare id 17 (Avoid)
## wield difficulty 160
## itemtype 1 values: meleeweapon=1, armor=2, clothing=4, jewelry=8, missle=100, caster=8000
## propertyint64 type =5, itembasexp
## itemxpstyle 320

##weenie types: ranged=3, melee=6, clothing=2

##clear out existing item levels,  item level type for all items excluding rares and sets
delete from ace_world.weenie_properties_int
where id in (select * from ( select i.id from ace_world.weenie_properties_int i
join ace_world.weenie w on w.class_Id = i.object_Id
join ace_world.weenie_properties_int it on it.object_Id = i.object_id and it.type = 1
where i.type in (319,320) and w.type in (6,2,3) and it.value in (1,2,4,100,8000)
and not exists (select * from ace_world.weenie_properties_int iset where iset.object_Id = i.object_id and iset.type = 265)
and not exists (select * from ace_world.weenie_properties_int ir where ir.object_Id = i.object_id and ir.type = 17) )as p) 
;
##clear out existing item base xp
delete from ace_world.weenie_properties_int64
where id in (select * from  ( select i.id from ace_world.weenie_properties_int64 i
join ace_world.weenie w on w.class_Id = i.object_Id
join ace_world.weenie_properties_int it on it.object_Id = i.object_id and it.type = 1
where i.type in (5) and w.type in (6,2,3) and it.value in (1,2,4,100,8000)
and not exists (select * from ace_world.weenie_properties_int iset where iset.object_Id = i.object_id and iset.type = 265)
and not exists (select * from ace_world.weenie_properties_int ir where ir.object_Id = i.object_id and ir.type = 17) )as p)
;

##base level insert with no wield requirement
insert ignore ace_world.weenie_properties_int (object_id, type, value)
select class_id, 319, 15 from ace_world.weenie w 
join ace_world.weenie_properties_int it on w.class_id = it.object_id and it.type = 1
left join ace_world.weenie_properties_int wd on w.class_id = wd.object_Id and wd.type = 160
where w.type = 35 and it.value = 32768
and not exists (select * from ace_world.weenie_properties_int iset where iset.object_Id = w.class_id and iset.type = 265)
and not exists (select * from ace_world.weenie_properties_int ir where ir.object_Id = w.class_id and ir.type = 17) 
and not exists (select * from ace_world.weenie_properties_int wd where wd.object_Id = w.class_id and wd.type = 160) 
##and wd.value >= 400 and wd.value < 500
;
##base xp style insert with no wield requirement
insert ignore ace_world.weenie_properties_int (object_id, type, value)
select class_id, 320, 2 from ace_world.weenie w 
join ace_world.weenie_properties_int it on it.object_Id = w.class_id and it.type = 1
left join ace_world.weenie_properties_int wd on w.class_id = wd.object_Id and wd.type = 160
where w.type = 35 and it.value = 32768
and not exists (select * from ace_world.weenie_properties_int iset where iset.object_Id = w.class_id and iset.type = 265)
and not exists (select * from ace_world.weenie_properties_int ir where ir.object_Id = w.class_id and ir.type = 17) 
and not exists (select * from ace_world.weenie_properties_int wd where wd.object_Id = w.class_id and wd.type = 160) 
##and wd.value >= 400 and wd.value < 500
;
##
insert ignore ace_world.weenie_properties_int64 (object_id, type, value) 
select class_id, 5, 250000  from ace_world.weenie w  
join ace_world.weenie_properties_int it on it.object_Id = w.class_id and it.type = 1
left join ace_world.weenie_properties_int wd on w.class_id = wd.object_Id and wd.type = 160
where w.type = 35 and it.value = 32768
and not exists (select * from ace_world.weenie_properties_int iset where iset.object_Id = w.class_id and iset.type = 265)
and not exists (select * from ace_world.weenie_properties_int ir where ir.object_Id = w.class_id and ir.type = 17) 
and not exists (select * from ace_world.weenie_properties_int wd where wd.object_Id = w.class_id and wd.type = 160) 
##and wd.value >= 400 and wd.value < 500
;

##verifying
select w.class_Id, w.class_Name, w.type, lv.value as 'MaxLevel', lv2.value 'XPStyle', it.value as 'ItemType', bx.Value
from ace_world.weenie w 
join ace_world.weenie_properties_int it on w.class_id = it.object_id and it.type = 1
left join ace_world.weenie_properties_int wd on w.class_id = wd.object_Id and wd.type = 160
left join ace_world.weenie_properties_int lv on w.class_id = lv.object_Id and lv.type = 319
left join ace_world.weenie_properties_int lv2 on w.class_Id = lv2.object_Id and lv2.type = 320
left join ace_world.weenie_properties_int64 bx on w.class_id = bx.object_Id and bx.type = 5
##where w.type in (6,2,3) and it.value in (1,2,4,100,8000)
where w.type = 3 ##and it.value = 256
and not exists (select * from ace_world.weenie_properties_int iset where iset.object_Id = w.class_id and iset.type = 265)
and not exists (select * from ace_world.weenie_properties_int ir where ir.object_Id = w.class_id and ir.type = 17) 
and not exists (select * from ace_world.weenie_properties_int wd where wd.object_Id = w.class_id and wd.type = 160) 

update ace_world.weenie_properties_int i set value = 10 where type = 319 and value >= 15
and not exists (select * from ace_world.weenie_properties_int iset where iset.object_Id = i.object_id and iset.type = 265)
and not exists (select * from ace_world.weenie_properties_int ir where ir.object_Id = i.object_id and ir.type = 17) 

update ace_world.weenie_properties_int set `value` = 1 where `type` = 320
and not exists (select * from ace_world.weenie_properties_int iset where iset.object_Id = w.class_id and iset.type = 265)
and not exists (select * from ace_world.weenie_properties_int ir where ir.object_Id = w.class_id and ir.type = 17) 
;

select * from ace_shard.biota_properties_float f
join ace_shard.biota b on b.id = f.object_id
join ace_world.weenie w on w.class_Id = b.weenie_Class_Id
 where f.type = 12 ## and f.value >= 1.3 and b.weenie_Type in (35);
 
 select * from ace_shard.biota_properties_int i
join ace_shard.biota b on b.id = i.object_id
join ace_world.weenie w on w.class_Id = b.weenie_Class_Id
 where i.type = 44 and i.value >= 60 and b.weenie_Type in (3,6);
 
 update ace_shard.biota_properties_int set value = (value-10) where type = 44 and object_id in (2147485212,
2147492167,
2147599091,
2147703644)
 update ace_shard.biota_properties_float set value = 1.1 where type = 29 and object_id in (2147731880)
 
 select * from 
 
 select * from ace_shard.character c 
 join ace_shard.character_properties_quest_registry q on c.id = q.character_id
 where q.quest_Name like 'Dynamic%'
 
 select * from ace_world.weenie where class_name like '%reformed%'
 
 
 select * from ace_world.weenie_properties_emote e
 join ace_world.weenie_properties_emote_action a on e.id = a.emote_id
 where e.object_id = 14410
 
 delete from ace_shard.biota where weenie_class_id = 41793
 
 select * from ace_world.weenie where class_Id = 300000
 