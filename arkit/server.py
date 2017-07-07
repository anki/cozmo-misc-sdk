#!/usr/bin/env python3

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

"""
This server will communicate with the AR app via websocket.
Cozmo will launch fireworks 3 times,
animation is controlled by the round number.
0. successful launch
1. successful launch
2. fail launch
3. grand finale
"""
import time
import socket
import asyncio
import threading
import asyncio
import sys
import random
import cozmo
from cozmo.util import degrees, distance_mm, speed_mmps, Pose
from cozmo.objects import CustomObject, CustomObjectMarkers, CustomObjectTypes
from lib.vision_cube import VisionCube
from lib.config import IP_ADDRESS, PORT

stopFlag = False

USE_VIEWER = False
USE_LOGGING = False
DEBUG_MODE = True
PURPLE = cozmo.lights.Color(rgb=(141, 7, 255))
BLUE = cozmo.lights.Color(rgb=(6, 6, 255))
YELLOW = cozmo.lights.Color(rgb=(255, 255, 7))
GREEN = cozmo.lights.Color(rgb=(0, 255, 0))
ORANGE = cozmo.lights.Color(rgb=(255, 177, 6))
PINK = cozmo.lights.Color(rgb=(255, 13, 144))

serverT = threading.Thread()


class TapCube(threading.Thread):

    def __init__(self):
        threading.Thread.__init__(self)
        self._round = 0
        self._cube = 0
        self._robot = None
        self._wait_for_tap_flag = False
        self._did_cozmo_tap_flag = False
        self._tap_action_finished = False
        self._animation_triggered = False
        self._trigger_message = False
        self._message = ""

        if USE_LOGGING:
            cozmo.setup_basic_logging()

        try:
            if USE_VIEWER:
                cozmo.connect_with_tkviewer(self.run)
            else:
                cozmo.connect(self.run)
        except cozmo.ConnectionError as e:
            sys.exit("A connection error occurred: %s" % e)

    async def run(self, coz_conn):
        """The run method runs once Cozmo is connected."""
        await self.set_up_cozmo(coz_conn)

        while True:
            await asyncio.sleep(0)
        pass

    async def set_up_cozmo(self, coz_conn):
        asyncio.set_event_loop(coz_conn._loop)
        self._robot = await coz_conn.wait_for_robot()

        global serverT
        serverT = threading.Timer(0, self.run_server).start()
        await self._robot.set_lift_height(0, duration=0.5).wait_for_completed()
        await self._robot.play_anim(
            "anim_launch_wakeup_02"
        ).wait_for_completed()
        await self._robot.play_anim(
            "anim_hiking_lookaround_01"
        ).wait_for_completed()
        await self._robot.play_anim(
            "anim_hiking_lookaround_03"
        ).wait_for_completed()
        await self._robot.play_anim(
            "anim_hiking_lookaround_02"
        ).wait_for_completed()
        await self._robot.set_head_angle(
            cozmo.util.degrees(0)
        ).wait_for_completed()
        self._robot.world.add_event_handler(
            cozmo.objects.EvtObjectTapped,
            self.on_object_tapped
        )

        # Uncomment to start in a specific sequence
        # self._round = 2
        while self._round < 3:
            print("self._round: %s" % self._round)
            await self.find_cube()
            print("selftap finish: %s" % self._tap_action_finished)
            if self._tap_action_finished:
                self._round = self._round + 1
                self._tap_action_finished = False
            if DEBUG_MODE:
                if self._round == 3:
                    self._round = 0

    async def find_cube(self):
        cubes = None
        if self._cube:
            self._cube.stop_light_chaser()
            self._cube.set_lights_off()
        look_around = self._robot.start_behavior(
            cozmo.behavior.BehaviorTypes.LookAroundInPlace
        )
        try:
            cubes = await self._robot.world.wait_until_observe_num_objects(
                1, cozmo.objects.LightCube
            )
            self._cube = cubes[0]
            look_around.stop()
        except TimeoutError:
            print("Didn't find a cube :-(")
            return False
        if self._round < 2:
            tap_anim = random.choice([
                "anim_pyramid_reacttocube_happy_high_01",
                "anim_speedtap_winround_intensity02_02"
            ])
            await self._robot.play_anim(tap_anim).wait_for_completed()
        if self._round > 2:
            return
        self._cube.start_light_chaser(GREEN)
        await self._robot.set_head_angle(
            cozmo.util.Angle(degrees=20.5)
        ).wait_for_completed()
        await self._robot.set_head_angle(
            cozmo.util.Angle(degrees=0)
        ).wait_for_completed()
        if self._cube:
            await self.go_to_cube(self._cube)

    async def go_to_cube(self, cube):
        await self._robot.set_lift_height(1, duration=0.5).wait_for_completed()
        await self._robot.go_to_object(
            cube, distance_mm(30.0)
        ).wait_for_completed()
        self._wait_for_tap_flag = True
        await self.coz_tap(cube)

    async def coz_tap(self, cube):
        if not self._wait_for_tap_flag:
            return
        threading.Timer(0.5, self.trigger_cozmo_tap).start()
        tap_anim = random.choice([
            "anim_speedtap_tap_01",
            "anim_speedtap_tap_02",
            "anim_speedtap_tap_03"
        ])
        await self._robot.play_anim(tap_anim).wait_for_completed()
        await self.see_fireworks(cube)

    async def see_fireworks(self, cube):
        if self._round != 2:
            await self._robot.drive_straight(
                distance_mm(-50), speed_mmps(80)
            ).wait_for_completed()
            await self._robot.set_lift_height(
                0, duration=0.3
            ).wait_for_completed()

        messages = [1, 2, 3]
        message = messages[self._round]

        # Tap unsuccessful
        if self._animation_triggered is False:
            print("Cube tap not detected.\
                Either Cozmo missed or cube has no power.")
            self._trigger_message = False
            await self._robot.play_anim(
                "anim_keepaway_losehand_01"
            ).wait_for_completed()

        # Cozmo taps cube and looks at fireworks
        if self._animation_triggered and self._round < 2:
            self.send_msg(message)

            # Cozmo acts excited
            if self._round == 0:
                await self._robot.set_head_angle(
                    cozmo.util.Angle(degrees=44.5), 1, 1, 1
                ).wait_for_completed()
                await asyncio.sleep(1)
                await self._robot.play_anim(
                    "anim_reacttoblock_react_01_head_angle_40"
                ).wait_for_completed()
                await self._robot.drive_straight(
                    distance_mm(-30), speed_mmps(30)
                ).wait_for_completed()
                self._animation_triggered = False
                self._tap_action_finished = True

            # Dud
            if self._round == 1:
                await self._robot.play_anim(
                    "anim_reacttocliff_turtlerollfail_03"
                ).wait_for_completed()
                self._animation_triggered = False
                self._tap_action_finished = True
        # Grand finale
        if self._animation_triggered and self._round == 2:
            await asyncio.sleep(0.5)
            await self._robot.play_anim(
                "anim_memorymatch_failhand_03"
            ).wait_for_completed()
            await asyncio.sleep(0.5)
            await self._robot.play_anim(
                "reacttoblock_triestoreach_01"
            ).wait_for_completed()
            if self._animation_triggered:
                self.send_msg(4)
            if self._animation_triggered:
                self.send_msg(3)
            await self._robot.play_anim(
                "anim_reacttoblock_react_01_head_angle_40"
            ).wait_for_completed()
            await self._robot.play_anim(
                "reacttoblock_reacttotopple_01"
            ).wait_for_completed()
            await self._robot.play_anim(
                "anim_reacttocliff_wheely_01"
            ).wait_for_completed()

            self._tap_action_finished = True
            await self.go_back_to_normal()
            self._animation_triggered = False
            self._tap_action_finished = True

    async def go_back_to_normal(self):
        self._cube.stop_light_chaser()
        self._cube.set_lights_off()
        await self._robot.play_anim(
            "anim_guarddog_getout_untouched_01"
        ).wait_for_completed()
        await self._robot.play_anim(
            "anim_guarddog_getout_timeout_01"
        ).wait_for_completed()
        await self._robot.play_anim(
            "anim_gotosleep_getin_01"
        ).wait_for_completed()

    def trigger_cozmo_tap(self):
        self._did_cozmo_tap_flag = True
        time.sleep(2)
        self._did_cozmo_tap_flag = False

    async def on_object_tapped(self, evt=None, obj=None, tap_count=None, **kwargs):
        if self._did_cozmo_tap_flag:
            if self._round < 2:
                self.send_msg(4)
            self._cube.stop_light_chaser()
            self._cube.set_lights_off()
            self._cube.rainbow_chaser()
            self._animation_triggered = True

    def send_msg(self, msg):
        self._message = str(msg).encode()
        self.sock.sendto(self._message, (IP_ADDRESS, PORT))
        self._trigger_message = True

    def run_server(self):
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        loop = asyncio.new_event_loop()
        asyncio.set_event_loop(loop)
        loop.run_forever()


if __name__ == "__main__":
    print("ARKit Server Started.")
    cozmo.world.World.light_cube_factory = VisionCube
    cozmoAction = TapCube()

    try:
        cozmoAction()

    except KeyboardInterrupt:
        stopFlag = True
        print("Exiting ARKit Server...")
