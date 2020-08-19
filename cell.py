

class Size:
    def __init__(self, width=28, heigth=28):
        self.width = width
        self.heigth= heigth

class Position:
    def __init__(self, x, y):
        self.x = x
        self.y = y

class LandType:
    DESERT= {
		'id': 0,
        'img': 'desert.png',
	}
    GRASS = {
		'id': 1,
        'img': 'grass.png',
	}
    FOREST= {
		'id': 2,
        'img': 'forest.png',
	}

class Cell:
    def __init__(self, uuid, position, size, landtype, adm=1, dip=1, mil=1):
        self.uuid = uuid
        self.position = position
        self.size = size
        self.landtype = landtype
        self.adm = adm
        self.dip = dip
        self.mil = mil