-- Populate UserProfileMappings by querying UserService database on port 3308
DELETE FROM UserProfileMappings;

-- Insert mappings (using shell variable substitution won't work in SQL, so we'll do this differently)
