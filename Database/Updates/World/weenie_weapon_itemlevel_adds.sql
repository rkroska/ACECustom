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
select class_id, 319, 35 from ace_world.weenie w 
join ace_world.weenie_properties_int it on w.class_id = it.object_id and it.type = 1
join ace_world.weenie_properties_int wd on w.class_id = wd.object_Id and wd.type = 160
where w.type in (6,2,3) and it.value in (1,2,4,100,8000)
and not exists (select * from ace_world.weenie_properties_int iset where iset.object_Id = w.class_id and iset.type = 265)
and not exists (select * from ace_world.weenie_properties_int ir where ir.object_Id = w.class_id and ir.type = 17) 
and wd.Value > 300 and wd.Value <= 500
;
##base xp style insert with no wield requirement
insert ignore ace_world.weenie_properties_int (object_id, type, value)
select class_id, 320, 2 from ace_world.weenie w 
join ace_world.weenie_properties_int it on it.object_Id = w.class_id and it.type = 1
left join ace_world.weenie_properties_int wd on w.class_id = wd.object_Id and wd.type = 160
where w.type in (6,2,3) and it.value in (1,2,4,100,8000)
and not exists (select * from ace_world.weenie_properties_int iset where iset.object_Id = w.class_id and iset.type = 265)
and not exists (select * from ace_world.weenie_properties_int ir where ir.object_Id = w.class_id and ir.type = 17) 
and wd.Value > 300 and wd.Value <= 500
;
##
insert ignore ace_world.weenie_properties_int64 (object_id, type, value) 
select class_id, 5, 400000  from ace_world.weenie w  
join ace_world.weenie_properties_int it on it.object_Id = w.class_id and it.type = 1
left join ace_world.weenie_properties_int wd on w.class_id = wd.object_Id and wd.type = 160
where w.type in (6,2,3) and it.value in (1,2,4,100,8000) 
and not exists (select * from ace_world.weenie_properties_int iset where iset.object_Id = w.class_id and iset.type = 265)
and not exists (select * from ace_world.weenie_properties_int ir where ir.object_Id = w.class_id and ir.type = 17) 
and wd.Value > 300 and wd.Value <= 500
;

