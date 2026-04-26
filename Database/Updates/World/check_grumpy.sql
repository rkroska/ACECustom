SELECT b.id, b.weenie_Class_Id, s.value as name
FROM biota b
LEFT JOIN biota_properties_string s ON b.id = s.object_Id AND s.type = 1
JOIN biota_properties_i_i_d iid ON b.id = iid.object_Id AND iid.type = 2 AND iid.value = 1342177281
ORDER BY b.id;
