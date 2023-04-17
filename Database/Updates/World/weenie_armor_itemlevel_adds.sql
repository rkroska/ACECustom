insert into ace_world.weenie_properties_int (type, value, object_id)
select 319, 10, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 2 and i2.value is null and not exists (select * from ace_world.weenie_properties_int i where i.type = 319 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int (type, value, object_id)
select 320, 1, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 2 and i2.value is null and not exists (select * from ace_world.weenie_properties_int i where i.type = 320 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int64 (type, value, object_id)
select 4, 0, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 2 and i2.value is null and not exists (select * from ace_world.weenie_properties_int64 i where i.type = 4 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int64 (type, value, object_id)
select 5, 250000, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 2 and i2.value is null and not exists (select * from ace_world.weenie_properties_int64 i where i.type = 5 and i.object_Id = w.class_Id);

#--------------------------------------------------------

insert into ace_world.weenie_properties_int (type, value, object_id)
select 319, 12, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 2 and i2.value <= 20 and not exists (select * from ace_world.weenie_properties_int i where i.type = 319 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int (type, value, object_id)
select 320, 1, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 2 and i2.value <= 20 and not exists (select * from ace_world.weenie_properties_int i where i.type = 320 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int64 (type, value, object_id)
select 4, 0, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 2 and i2.value <= 20 and not exists (select * from ace_world.weenie_properties_int64 i where i.type = 4 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int64 (type, value, object_id)
select 5, 250000, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 2 and i2.value <= 20 and not exists (select * from ace_world.weenie_properties_int64 i where i.type = 5 and i.object_Id = w.class_Id);

#------------------------------------------------------------

insert into ace_world.weenie_properties_int (type, value, object_id)
select 319, 14, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 2 and i2.value between 21 and 49 and not exists (select * from ace_world.weenie_properties_int i where i.type = 319 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int (type, value, object_id)
select 320, 1, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 2 and i2.value between 21 and 49 and not exists (select * from ace_world.weenie_properties_int i where i.type = 320 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int64 (type, value, object_id)
select 4, 0, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 2 and i2.value between 21 and 49 and not exists (select * from ace_world.weenie_properties_int64 i where i.type = 4 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int64 (type, value, object_id)
select 5, 250000, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 2 and i2.value between 21 and 49 and not exists (select * from ace_world.weenie_properties_int64 i where i.type = 5 and i.object_Id = w.class_Id);

#------------------------------------------------------------

insert into ace_world.weenie_properties_int (type, value, object_id)
select 319, 15, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 2 and i2.value between 50 and 199 and not exists (select * from ace_world.weenie_properties_int i where i.type = 319 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int (type, value, object_id)
select 320, 1, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 2 and i2.value between 50 and 199 and not exists (select * from ace_world.weenie_properties_int i where i.type = 320 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int64 (type, value, object_id)
select 4, 0, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 2 and i2.value between 50 and 199 and not exists (select * from ace_world.weenie_properties_int64 i where i.type = 4 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int64 (type, value, object_id)
select 5, 250000, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 2 and i2.value between 50 and 199 and not exists (select * from ace_world.weenie_properties_int64 i where i.type = 5 and i.object_Id = w.class_Id);

#------------------------------------------------------------

insert into ace_world.weenie_properties_int (type, value, object_id)
select 319, 30, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 2 and i2.value between 200 and 301 and not exists (select * from ace_world.weenie_properties_int i where i.type = 319 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int (type, value, object_id)
select 320, 1, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 2 and i2.value between 200 and 301 and not exists (select * from ace_world.weenie_properties_int i where i.type = 320 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int64 (type, value, object_id)
select 4, 0, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 2 and i2.value between 200 and 301 and not exists (select * from ace_world.weenie_properties_int64 i where i.type = 4 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int64 (type, value, object_id)
select 5, 400000, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 2 and i2.value between 200 and 301 and not exists (select * from ace_world.weenie_properties_int64 i where i.type = 5 and i.object_Id = w.class_Id);

#------------------------------------------------------------

insert into ace_world.weenie_properties_int (type, value, object_id)
select 319, 80, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 2 and i2.value >= 301 and not exists (select * from ace_world.weenie_properties_int i where i.type = 319 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int (type, value, object_id)
select 320, 1, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 2 and i2.value >= 301 and not exists (select * from ace_world.weenie_properties_int i where i.type = 320 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int64 (type, value, object_id)
select 4, 0, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 2 and i2.value >= 301 and not exists (select * from ace_world.weenie_properties_int64 i where i.type = 4 and i.object_Id = w.class_Id);

insert into ace_world.weenie_properties_int64 (type, value, object_id)
select 5, 2000000, w.class_Id #, w.class_Name, i2.value 
from ace_world.weenie w
left join ace_world.weenie_properties_int i2 on i2.object_id = w.class_id and i2.type = 160
where w.type = 2 and i2.value >= 301 and not exists (select * from ace_world.weenie_properties_int64 i where i.type = 5 and i.object_Id = w.class_Id);

#------------------------------------------------------------


#null
#20,30,40,50
#250
#325
80
85
45
30
2
225
60
290
325
230
270
40
50
315
285
170
120
90
300
20
200
100
55
70
175
35
65
280
150
145
375
130
180
601
1
101
1001
301
125
115