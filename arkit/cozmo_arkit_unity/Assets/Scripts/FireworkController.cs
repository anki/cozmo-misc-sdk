// Copyright (c) 2017 Anki, Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License in the file LICENSE.txt or at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Cozmo
{
	namespace AR
	{
		using System.Collections;
		using System.Collections.Generic;
		using UnityEngine;

		public class FireworkController : MonoBehaviour 
		{
			public enum LaunchType : int
			{
				None, 
				Single, 
				Dud,
				Finale,
				Fuse,
				Count
			}

			// Returns random color from palette
			static public Color GetColor()
			{
				return sInstance == null ? Color.white : ( sInstance.colors == null ? Color.white : sInstance.colors[ Random.Range( 0, sInstance.colors.Length ) ] );
			}

			// Returns random explosion type
			static public AudioClip GetExplosion()
			{
				return sInstance == null ? null : ( sInstance.explosions == null ? null : sInstance.explosions[ Random.Range( 0, sInstance.explosions.Length ) ] );
			}

			// Returns duration of rocket flight
			static public float GetRocketDuration()
			{
				return sInstance == null ? 1.0f : sInstance.rocketDuration;
			}

			// Returns easing exponent
			static public float GetRocketEasing()
			{
				return sInstance == null ? 1.0f : ( sInstance.rocketEasing < 3.0f ? 1.0f : sInstance.rocketEasing );
			}

			// Returns a random scale value
			static public float RandomScale()
			{
				return sInstance == null ? Random.value : Random.Range( sInstance.scaleMin, sInstance.scaleMax );
			}

			// Singleton to give Firework instances access to parameters
			static private FireworkController sInstance = null;

			[Tooltip( "Prefab of dud animation in assets" )]
			public GameObject dudPrefab = null;
			[Tooltip( "Prefab of firework animation in assets" )]
			public GameObject fireworkPrefab = null;
			[Tooltip( "Prefab of fuse animation in assets" )]
			public GameObject fusePrefab = null;

			[Tooltip( "Firework color palette" )]
			public Color[] colors = null;
			[Tooltip( "Array of firework explosion audio clips" )]
			public AudioClip[] explosions = null;

			[Tooltip( "Prefab of dud animation in assets" )]
			public float launchDelay = 0.1f;
			[Tooltip( "Duration of rocket animation" )]
			public float rocketDuration = 1.0f;
			[Tooltip( "Rocket animation easing exponent. Below 3.0 is linear." )]
			public float rocketEasing = 3.0f;

			[Tooltip( "Decay value for interval between fireworks during finale" )]
			public float finaleDelayDecay = 0.9f;
			[Tooltip( "Maximum number of fireworks in finale" )]
			public int finaleMaxCount = 45;
			[Tooltip( "Minimum number of fireworks in finale" )]
			public int finaleMinCount = 35;
			[Tooltip( "Maximum delay between first and second firework in finale. Subsquent intervals are multiplied by finaleDelayDecay." )]
			public float finaleMaxDelay = 3.0f;
			[Tooltip( "Minimum delay between first and second firework in finale. Subsquent intervals are multiplied by finaleDelayDecay." )]
			public float finaleMinDelay = 2.8f;
			[Tooltip( "Maximum firework explosion scale" )] 
			public float scaleMax = 0.1f;
			[Tooltip( "Minimum firework explosion scale" )]
			public float scaleMin = 0.05f;

			private int mFinaleCount = 0;
			private float mFinaleDelay = 0.0f;
			private float mFinaleMaxDelay = 2.0f;
			private float mFinaleMinDelay = 1.8f;
			private float mFinaleStartTime = 0.0f;
			private float mLaunchTime = 0.0f;
			private LaunchType mType = LaunchType.None;

			// Starts launch sequence
			static public void Launch( LaunchType t )
			{
				if ( sInstance != null ) {

					// Mark the launch time and type. We don't launch 
					// just yet because Cozmo needs to delay by `launchDelay`
					// to look up.
					sInstance.mLaunchTime = Time.realtimeSinceStartup;
					sInstance.mType = t;
				}
			}

			private void Awake()
			{
				sInstance = this;
			}

			private void Update()
			{
				// Use keyboard keys to test launch types
#if UNITY_EDITOR
				if ( Input.GetKeyUp( KeyCode.Alpha1 ) ) {
					FireworkController.Launch( FireworkController.LaunchType.Single );
				} else if ( Input.GetKeyUp( KeyCode.Alpha2 ) ) {
					FireworkController.Launch( FireworkController.LaunchType.Dud );
				} else if ( Input.GetKeyUp( KeyCode.Alpha3 ) ) {
					FireworkController.Launch( FireworkController.LaunchType.Finale );
				} else if ( Input.GetKeyUp( KeyCode.Alpha4 ) ) {
					FireworkController.Launch( FireworkController.LaunchType.Fuse );
				}
#endif

				// Time to launch!
				float e = Time.realtimeSinceStartup;
				if ( mLaunchTime > 0.0f && e - mLaunchTime > launchDelay ) {
					switch ( mType ) {
					case LaunchType.Dud:

						// Fire the dud
						if ( dudPrefab != null ) {
							ARScene.Append( dudPrefab );
						}

						break;
					case LaunchType.Finale:

						// Record the original delay values
						mFinaleMaxDelay = finaleMaxDelay;
						mFinaleMinDelay = finaleMinDelay;

						// Choose a random number of fireworks and start the finale
						mFinaleCount = finaleMinCount + (int)( Random.value * Mathf.Abs( (float)finaleMaxCount - (float)finaleMinCount ) );
						mFinaleStartTime = Time.realtimeSinceStartup;

						break;
					case LaunchType.Fuse:

						// Start the fuse animation
						ARScene.Append( fusePrefab );
						break;

					case LaunchType.Single:

						// Shoot off a firework
						ARScene.Append( fireworkPrefab );

						break;
					}

					// Ready for next launch
					mLaunchTime = 0.0f;
					mType = LaunchType.None;
				}

				// Grand finale timing logic
				if ( mFinaleStartTime > 0.0f && e - mFinaleStartTime > mFinaleDelay ) {
					Launch( LaunchType.Single );
					mFinaleDelay = finaleMinDelay + Random.value * ( Mathf.Abs( finaleMaxDelay - finaleMinDelay ) );
					finaleMaxDelay *= finaleDelayDecay;
					finaleMinDelay *= finaleDelayDecay;
					mFinaleStartTime = e;
					--mFinaleCount;
				}

				// Restore delay values when the finale is complete
				if ( mFinaleCount <= 0 ) {
					finaleMaxDelay = mFinaleMaxDelay;
					finaleMinDelay = mFinaleMinDelay;
					mFinaleStartTime = 0.0f;
				}

			}
		}
	}
}
 
