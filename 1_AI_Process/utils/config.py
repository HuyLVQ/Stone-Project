import sys

MMF_TAGNAME = sys.argv[1]
UI_2_AI_EVENT_TAGNAME = sys.argv[2] 
AI_2_UI_EVENT_TAGNAME = sys.argv[3] 
IMAGE_WIDTH, IMAGE_HEIGHT = int(sys.argv[5]), int(sys.argv[6])

WRITE_OFFSET = 0                                ## Offset value from the beginning address of shared memory region
READ_OFFSET = 10_000_000                        ## Offset value from the beginning address of shared memory region
MEMORY_ALLOCATION = 20_000_000                  ## Total memory allocated for MMF IPC
MMF_TIMEOUT = 10_000_000

RATIO_SCALE = 1/19
TYPE_COLORS = {
                "MiSang": (255, 0, 0),    
                "1x2": (0, 255, 0),      
                "2x4": (0, 255, 255),   
                "4x6": (0, 0, 255),     
                "Khac": (128, 128, 128)             
              }