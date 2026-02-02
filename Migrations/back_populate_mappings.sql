-- Back-populate UserProfileMappings from UserService.Profiles
-- Connects Keycloak user IDs to ProfileIds for match validation

-- Clear existing mappings (if any)
TRUNCATE TABLE UserProfileMappings;

-- Insert mappings from UserService database
INSERT INTO UserProfileMappings (ProfileId, UserId, CreatedAt)
SELECT 
    p.Id AS ProfileId,
    p.UserId AS UserId,
    NOW() AS CreatedAt
FROM user_service_db.Profiles p
WHERE p.UserId IS NOT NULL
ON DUPLICATE KEY UPDATE CreatedAt = NOW();

SELECT 'Populated', COUNT(*) AS MappingCount FROM UserProfileMappings;
