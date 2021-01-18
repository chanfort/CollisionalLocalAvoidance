# Collisional Local Avoidance

This project was made to investigate performace of local avoidance used in Unite Austin Technical Presentation (https://github.com/Unity-Technologies/UniteAustinTechnicalPresentation). Unity forum discussion can be found here: https://forum.unity.com/threads/collisional-local-avoidance-extracted.599023/.

Project contains multiple scenes for testing Hash based and KdTree based local avoidances. These are specifications of the scenes:

1. 10kWrite - test which runs simulation with 10k units and writes framerate statistics to file.
2. 20kWrite - test which runs simulation with 20k units and writes framerate statistics to file.
3. 30kWrite - test which runs simulation with 30k units and writes framerate statistics to file.
4. 40kWrite - test which runs simulation with 40k units and writes framerate statistics to file.
5. kdTreeTests - test which renders smaller amount of units in KdTree based avoidance simulation in order to visualise and inspect how simulation works.
6. NonRenderedCenterPassage - Hash based avoidance simulation similar to 1. - 4. cases without writing to file. Could be useful to quickly test different number of units without editing file write examples.
7. RenderedCenterPassage - test which renders smaller amount of units in Hash based avoidance simulation in order to visualise and inspect how simulation works.
8. RenderedCenterPassageCollisionless - test which renders smaller amount of units without local avoidance in order to visualise and inspect how simulation works.
