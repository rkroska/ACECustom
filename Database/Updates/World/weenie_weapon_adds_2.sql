    select count(*), wd.value from ace_world.weenie w 
join ace_world.weenie_properties_int it on w.class_Id = it.object_Id and it.type = 1
left join ace_world.weenie_properties_int wd on w.class_id = wd.object_Id and wd.type = 160
left join ace_world.weenie_properties_int64 xp on w.class_id = xp.object_Id and xp.type = 5
left join ace_world.weenie_properties_int st on w.class_id = st.object_Id and st.type = 320
left join ace_world.weenie_properties_int ml on w.class_id = ml.object_id and ml.type = 319
where w.type = 6 and it.value = 1
and not exists (select * from ace_world.weenie_properties_int iset where iset.object_Id = w.class_Id and iset.type = 265)
and not exists (select * from ace_world.weenie_properties_int ir where ir.object_Id = w.class_Id and ir.type = 17)
group by wd.value
order by count(*) desc
;

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
join ace_world.weenie_properties_int it on it.object_Id = w.class_id and it.type = 1
left join ace_world.weenie_properties_int wd on w.class_id = wd.object_Id and wd.type = 160
where w.type in (6,2,3) and it.value in (1,2,4,100,8000) and wd.value is null
and not exists (select * from ace_world.weenie_properties_int iset where iset.object_Id = w.class_id and iset.type = 265)
and not exists (select * from ace_world.weenie_properties_int ir where ir.object_Id = w.class_id and ir.type = 17) 
;
##base xp style insert with no wield requirement
insert ignore ace_world.weenie_properties_int (object_id, type, value)
select class_id, 320, 2 from ace_world.weenie w 
join ace_world.weenie_properties_int it on it.object_Id = w.class_id and it.type = 1
left join ace_world.weenie_properties_int wd on w.class_id = wd.object_Id and wd.type = 160
where w.type in (6,2,3) and it.value in (1,2,4,100,8000) and wd.value is null
and not exists (select * from ace_world.weenie_properties_int iset where iset.object_Id = w.class_id and iset.type = 265)
and not exists (select * from ace_world.weenie_properties_int ir where ir.object_Id = w.class_id and ir.type = 17) 
;
##
insert ignore ace_world.weenie_properties_int64 (object_id, type, value) 
select class_id, 5, 250000  from ace_world.weenie w  
join ace_world.weenie_properties_int it on it.object_Id = w.class_id and it.type = 1
left join ace_world.weenie_properties_int wd on w.class_id = wd.object_Id and wd.type = 160
where w.type in (6,2,3) and it.value in (1,2,4,100,8000) and wd.value is null
and not exists (select * from ace_world.weenie_properties_int iset where iset.object_Id = w.class_id and iset.type = 265)
and not exists (select * from ace_world.weenie_properties_int ir where ir.object_Id = w.class_id and ir.type = 17) 
;


