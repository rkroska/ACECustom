insert into ace_world.weenie_properties_int (type, value, object_id)
select 319, 10, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 6 and i2.value is null and not exists (select * from ace_world.weenie_properties_int i where i.type = 319 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int (type, value, object_id)
select 320, 1, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 6 and i2.value is null and not exists (select * from ace_world.weenie_properties_int i where i.type = 320 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int64 (type, value, object_id)
select 4, 0, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 6 and i2.value is null and not exists (select * from ace_world.weenie_properties_int64 i where i.type = 4 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int64 (type, value, object_id)
select 5, 250000, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 6 and i2.value is null and not exists (select * from ace_world.weenie_properties_int64 i where i.type = 5 and i.object_Id = w.class_Id);

#--------------------------------------------------------

insert into ace_world.weenie_properties_int (type, value, object_id)
select 319, 12, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 6 and i2.value = 20 and not exists (select * from ace_world.weenie_properties_int i where i.type = 319 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int (type, value, object_id)
select 320, 1, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 6 and i2.value = 20 and not exists (select * from ace_world.weenie_properties_int i where i.type = 320 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int64 (type, value, object_id)
select 4, 0, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 6 and i2.value = 20 and not exists (select * from ace_world.weenie_properties_int64 i where i.type = 4 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int64 (type, value, object_id)
select 5, 250000, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 6 and i2.value = 20 and not exists (select * from ace_world.weenie_properties_int64 i where i.type = 5 and i.object_Id = w.class_Id);

#------------------------------------------------------------

insert into ace_world.weenie_properties_int (type, value, object_id)
select 319, 14, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 6 and i2.value = 30 and not exists (select * from ace_world.weenie_properties_int i where i.type = 319 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int (type, value, object_id)
select 320, 1, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 6 and i2.value = 30 and not exists (select * from ace_world.weenie_properties_int i where i.type = 320 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int64 (type, value, object_id)
select 4, 0, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 6 and i2.value = 30 and not exists (select * from ace_world.weenie_properties_int64 i where i.type = 4 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int64 (type, value, object_id)
select 5, 250000, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 6 and i2.value = 30 and not exists (select * from ace_world.weenie_properties_int64 i where i.type = 5 and i.object_Id = w.class_Id);

#------------------------------------------------------------

insert into ace_world.weenie_properties_int (type, value, object_id)
select 319, 15, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 6 and i2.value = 40 and not exists (select * from ace_world.weenie_properties_int i where i.type = 319 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int (type, value, object_id)
select 320, 1, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 6 and i2.value = 40 and not exists (select * from ace_world.weenie_properties_int i where i.type = 320 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int64 (type, value, object_id)
select 4, 0, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 6 and i2.value = 40 and not exists (select * from ace_world.weenie_properties_int64 i where i.type = 4 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int64 (type, value, object_id)
select 5, 250000, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 6 and i2.value = 40 and not exists (select * from ace_world.weenie_properties_int64 i where i.type = 5 and i.object_Id = w.class_Id);

#------------------------------------------------------------

insert into ace_world.weenie_properties_int (type, value, object_id)
select 319, 80, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 6 and i2.value = 325 and not exists (select * from ace_world.weenie_properties_int i where i.type = 319 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int (type, value, object_id)
select 320, 1, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 6 and i2.value = 325 and not exists (select * from ace_world.weenie_properties_int i where i.type = 320 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int64 (type, value, object_id)
select 4, 0, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 6 and i2.value = 325 and not exists (select * from ace_world.weenie_properties_int64 i where i.type = 4 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int64 (type, value, object_id)
select 5, 2000000, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 6 and i2.value = 325 and not exists (select * from ace_world.weenie_properties_int64 i where i.type = 5 and i.object_Id = w.class_Id);

#------------------------------------------------------------

insert into ace_world.weenie_properties_int (type, value, object_id)
select 319, 120, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 6 and i2.value > 325 and not exists (select * from ace_world.weenie_properties_int i where i.type = 319 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int (type, value, object_id)
select 320, 1, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 6 and i2.value > 325 and not exists (select * from ace_world.weenie_properties_int i where i.type = 320 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int64 (type, value, object_id)
select 4, 0, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 6 and i2.value > 325 and not exists (select * from ace_world.weenie_properties_int64 i where i.type = 4 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int64 (type, value, object_id)
select 5, 10000000, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 6 and i2.value > 325 and not exists (select * from ace_world.weenie_properties_int64 i where i.type = 5 and i.object_Id = w.class_Id);

#------------------------------------------------------------


#null
#20,30,40,50
#250
#325