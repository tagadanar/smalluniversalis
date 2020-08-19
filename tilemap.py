import pygame
import random
import sys
import json
from pygame.locals import *

class Map:
    cells = [] # The actual tilemap

    def __init__(self, textures, tilesize, width, height, borders=0):
        self.textures = textures # List of textures in dictionary format
        self.tilesize = tilesize + borders # The size of a tile
        self.width = width # How many tiles wide the map is
        self.height = height # How many tile high the map is
        self.window_size = [self.tilesize*self.width-borders,
                            self.tilesize*self.height-borders]
        self.mapsize = [self.width, self.height]

    def generate(self, cells):
        for cell in cells:
            self.cells.append(cell)
    
    def json_dump(self, file_name):
        """Save the tilemap as a JSON file"""
        with open(file_name, 'w') as f:
            dumped = json.dumps(self.cells)
            f.write(dumped)

    def json_load(self, file_name):
        """Load a JSON file"""
        with open(file_name, 'r') as f:
            self.cells = json.load(f.read())

    def draw(self, display):
        """Draw the tilemap"""
        try:
            for cell in self.cells:
                display.blit(self.textures[cell.landtype['id']],
                                    (cell.position.x*self.tilesize,
                                    cell.position.y*self.tilesize))
        except KeyError:
            print("ERROR: the draw() function is trying to load a texture that doesn't exist. Are you sure it's in the dictionary?\n"+str(self.textures) + "\n")
            sys.exit()
        except TypeError:
            print("ERROR: the draw() function is trying to draw something that isn't a pygame.Surface object.\n\n"+str(self.textures) + "\n")
            sys.exit()
        except IndexError:
            print("ERROR: the tilemap seems to be empty.\n\n"+str(self.tilemap) + "\n")
            sys.exit()