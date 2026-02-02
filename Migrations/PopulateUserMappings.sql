-- Populate UserProfileMappings from existing swipes + user data
-- This is a one-time migration to enable match validation
DELETE FROM UserProfileMappings;

-- Note: In production, this would query from UserService.Profiles
-- In demo mode, we'll populate this dynamically when users swipe
