# Copyright (c) 2017 Anki, Inc.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License in the file LICENSE.txt or at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

import asyncio
import io
import random
import cozmo
from cozmo.lights import Color
from cozmo.objects import CustomObject, CustomObjectMarkers, CustomObjectTypes

CYAN = Color(name="cyan", int_color=0x00ffffff)
PINK = Color(name="magenta", int_color=0xff00ffff)
YELLOW = Color(name="yellow", int_color=0xffff00ff)
GREEN = Color(name="green", int_color=0x00ff00ff)
RED = Color(name="red", int_color=0xff0000ff)
BLUE = Color(name="blue", int_color=0x0000ffff)
WHITE = Color(name="white", int_color=0xffffffff)
OFF = Color(name="off")

class VisionCube(cozmo.objects.LightCube):
    def __init__(self, *a, **kw):
        super().__init__(*a, **kw)
        self._chaser = None
        self._cube = cozmo.objects.LightCube
        self._color = cozmo.lights.off

    def color(self):
        return self._color

    def set_color(self, value: cozmo.lights.Color):
        self._color = value
        self.set_lights(cozmo.lights.Light(value))

    def start_light_chaser(self, value: cozmo.lights.Color):
        self._color = value
        if self._chaser:
            raise ValueError("Light chaser already running")
        async def _chaser():
            while True:
                for i in range(4):
                    cols = [cozmo.lights.off_light] * 4
                    cols[i] = cozmo.lights.Light(value)
                    self.set_light_corners(*cols)
                    await asyncio.sleep(0.1, loop=self._loop)
        self._chaser = asyncio.ensure_future(_chaser(), loop=self._loop)

    def rainbow_chaser(self):
        if self._chaser:
            raise ValueError("Light chaser already running")
        async def _chaser():
            while True:
                for i in range(4):
                    values = [CYAN,YELLOW,BLUE,RED,WHITE,PINK,GREEN]
                    r = random.randint(0,6)
                    cols = [cozmo.lights.off_light] * 4
                    cols[i] = cozmo.lights.Light(values[r])
                    self.set_light_corners(*cols)
                    await asyncio.sleep(0.1, loop=self._loop)
        self._chaser = asyncio.ensure_future(_chaser(), loop=self._loop)

    def stop_light_chaser(self):
        if self._chaser:
            self._chaser.cancel()
            self._chaser = None

    def turn_of_lights(self):
        self.set_lights_off()

if __name__ =='__main__':
    cozmo.world.World.light_cube_factory = VisionCube
