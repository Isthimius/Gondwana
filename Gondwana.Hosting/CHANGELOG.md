# Changelog

All notable changes to this project will be documented in this file.


# v2.5.2 - July 26, 2026




# v2.5.1 - July 19, 2026



## Added
- Implement widget input handling and routing




# v2.5.0 - July 09, 2026



## Added
- Move SplashScreen and add Gondwana.Widgets
- Add base classes for draggable widgets
- Improve template structure and logging




# v2.4.3 - June 16, 2026




# v2.4.2 - June 11, 2026




# v2.4.1 - June 09, 2026




# v2.4.0 - June 09, 2026



## Refactoring
- Improve Tilesheet and region handling




# v2.3.0 - May 20, 2026



## Added
- Wire touch adapter into GameHost lifecycle same as keyboard and mouse
- Replace SpotSplashForm with platform-agnostic DirectImage splash



## Fixed
- Fix for 2.1.1 patch
- Reorder GameHostBase.Initialize so Scene is set before InitializationComplete fires
- Add log warnings to SplashScreen.TryCreate for missing/invalid image file
- Delay Spot startup visuals/music until post-splash and hold Gondwana splash for 3s
- Cache splash image as SKImage and fix disposing event handler type
- Decode splash asset once and cache as SKImage



## Other Changes
- Moving project tags explicitly to individual projects
- Add splash post-fade-in callback
- Refine splash callback docs
- Clarify splash callback comment



# v2.1.0 - April 20, 2026



## Other Changes
- GameHost for standard implementation
- Moving assets to folder; minor cleanup; adding reference to Engine instance in GameHostBase
- SpotGame events
- Copying README; solution org
- Project settings and files for NuGet publication
- Per project README files



