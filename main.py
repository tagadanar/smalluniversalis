import pygame
import sys
import random
from tilemap import Map
from cell import Cell, Position, Size, LandType

def main():
    FRAME_RATE = 30 # 30 fps
    UPDATE_RATE = 1000 # update tick every 1000ms

    pygame.init()
    # init config map
    tilemap = Map({
        # Load the textures
        0: pygame.image.load("image/desert.png"),
        1: pygame.image.load("image/grass.png"),
        2: pygame.image.load("image/forest.png")},
                    
        28, # Tile size
        2, # Width
        2, # Height
        2) # Border size (default is 0)
    # choose window size
    DISPLAYSURF = pygame.display.set_mode((800, 600))
    # define every cell
    tilemap.generate([
        Cell(0, Position(0,0), Size(), LandType.DESERT),
        Cell(1, Position(1,0), Size(), LandType.FOREST),
        Cell(2, Position(0,1), Size(), LandType.GRASS),
        Cell(3, Position(1,1), Size(), LandType.FOREST),
    ])

    
    # It is best to use the value between pygame.USEREVENT and pygame.NUMEVENTS.
    # pygame.USEREVENT => 24
    # pygame.NUMEVENTS => 32
    UPDATE_EVENT = pygame.USEREVENT +1
    pygame.time.set_timer(UPDATE_EVENT, UPDATE_RATE) # updating the game of a day every UPDATE_RATE ms

    # main loop
    time_elapsed_since_last_action = 0
    clock = pygame.time.Clock()
    while True:
        # check for events
        for event in pygame.event.get():
            if event.type == pygame.QUIT:
                pygame.quit()
                sys.exit()
            elif event.type == UPDATE_EVENT:
                # TODO update all here !
                # ftm random change to test my code
                ltype = random.randint(0,2)
                if ltype == 0:
                    tilemap.cells[random.randint(0,3)].landtype = LandType.FOREST
                if ltype == 1:
                    tilemap.cells[random.randint(0,3)].landtype = LandType.DESERT
                if ltype == 2:
                    tilemap.cells[random.randint(0,3)].landtype = LandType.GRASS

        # update view
        tilemap.draw(DISPLAYSURF) # Draw the map
        pygame.display.update() # update the screen
        # flip() reminder https://www.quora.com/Whats-the-difference-between-pygame-display-update-and-pygame-display-flip

        # flushing print for debug
        sys.stdout.flush()

        # Should be set at the end of the first turn
        clock.tick(FRAME_RATE) # FRAME_RATE is the FPS, dt is the elapsed time
        # FRAME_RATE throttles the speed, so your computer can work efficiently

if __name__ == "__main__":
	main()