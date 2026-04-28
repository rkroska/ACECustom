SELECT bp.type, bp.value
FROM `character` c
JOIN biota_properties_bool bp ON c.id = bp.object_Id
WHERE c.name = 'Grumpy'
  AND bp.type IN (20, 68, 50028);
