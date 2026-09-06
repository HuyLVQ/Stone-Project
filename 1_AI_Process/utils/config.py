import sys

# Worker processes receive MMF names, map size, dimensions, and broker
# endpoint. Keep the legacy values only for compatibility with old tools.
MMF_TAGNAME = sys.argv[1] if len(sys.argv) > 1 else ""
UI_2_AI_EVENT_TAGNAME = ""
AI_2_UI_EVENT_TAGNAME = ""
IMAGE_WIDTH = int(sys.argv[4]) if len(sys.argv) > 5 else 1_920
IMAGE_HEIGHT = int(sys.argv[5]) if len(sys.argv) > 5 else 1_200
IS_DEBUG_MODE = False

WRITE_OFFSET = 0                                ## Offset value from the beginning address of shared memory region
READ_OFFSET = 10_000_000                        ## Offset value from the beginning address of shared memory region
MEMORY_ALLOCATION = 20_000_000                  ## Total memory allocated for MMF IPC
MMF_TIMEOUT = 10_000_000

RATIO_SCALE = 1/18.82
TYPE_COLORS = {
                "MiSang": (255, 120, 0),    
                "1x2": (0, 220, 0),      
                "2x4": (0, 220, 255),     
                "4x6": (0, 80, 255),         
                "Khac": (180, 180, 180)          
              }


FEATURE_NAMES = [
                  'count_mi_sang', 'count_1x2', 'count_2x4', 'count_4x6',
                  'area_cm2_mi_sang', 'area_cm2_1x2', 'area_cm2_2x4', 'area_cm2_4x6', 
                  'perimeter_cm_mi_sang', 'perimeter_cm_1x2', 'perimeter_cm_2x4', 'perimeter_cm_4x6'
                ]
