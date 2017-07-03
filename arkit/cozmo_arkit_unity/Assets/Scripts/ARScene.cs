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

		public class ARScene : MonoBehaviour
		{
			// Singleton
			static private ARScene sInstance = null;

			// Returns true if scene is visible and active
			static public bool isVisible
			{
				get {
					return sInstance == null ? false : sInstance.mVisible;
				}
			}

			[Tooltip( "Music fade in time" )]
			public float audioFadeDuration = 4.0f;

			[Tooltip( "Vertical offset for game objects appended to the scene" )]
			public float offset = 0.0f;

			private List<GameObject> mAnimations = new List<GameObject>();
			private AudioSource mAudioSource = null;
			private Vector3 mPosition = Vector3.zero;
			private Quaternion mRotation = Quaternion.identity;
			private Vector3 mScale = Vector3.one;
			private Vector3 mScaleOrigin = Vector3.one;
			private float mStartTime = 0.0f;
			private bool mVisible = false;
			private float mVolume = 0.0f;

			// Append a game object to the scene
			static public void Append( GameObject prefab )
			{
				if ( sInstance != null && prefab != null ) {
					GameObject go = Instantiate( prefab ) as GameObject;
					if ( go != null ) {

						// Mark fuse animation complete when any new prefab is added
						foreach ( GameObject anim in sInstance.mAnimations ) {
							if ( anim != null ) {
								Fuse f = anim.GetComponent<Fuse>();
								if ( f != null ) {
									f.isComplete = true;
								}
							}
						}

						// Put game object in hierarchy and fit it to the scene
						go.transform.parent = sInstance.transform;
						go.transform.position = sInstance.transform.position;
						Vector3 p = go.transform.position;
						p.y += sInstance.offset;
						go.transform.position = p;
						go.transform.rotation = sInstance.transform.localRotation * go.transform.localRotation;
						Vector3 s = sInstance.mScale;
						s.x *= go.transform.localScale.x;
						s.y *= go.transform.localScale.y;
						s.z *= go.transform.localScale.z;
						go.transform.localScale = s;

						// Add the new game object to our list of animations
						sInstance.mAnimations.Add( go );
					}
				}
			}

			// Sets the scene's position
			static public void SetPosition( Vector3 v )
			{
				if ( sInstance != null ) {
					sInstance.mPosition = v;
				}
			}

			// Sets the scene's rotation
			static public void SetRotation( Quaternion q )
			{
				if ( sInstance != null ) {
					sInstance.mRotation = q;
				}
			}

			// Sets the scene's scale
			static public void SetScale( Vector3 v )
			{
				if ( sInstance != null ) {
					sInstance.mScale = v;
				}
			}

			// Hide the scene by disabling all renderers
			static public void Hide()
			{
				if ( sInstance != null ) {
					foreach ( Renderer r in sInstance.GetComponentsInChildren<Renderer>() ) {
						if ( r != null ) {
							r.enabled = false;
						}
					}
					sInstance.mStartTime = 0.0f;
					sInstance.mVisible = false;
				}
			}

			// Hide the scene by enabling all renderers
			static public void Show()
			{
				if ( sInstance != null ) {
					foreach ( Renderer r in sInstance.GetComponentsInChildren<Renderer>() ) {
						if ( r != null ) {
							r.enabled = true;
						}
					}

					// Set the visibility flag
					sInstance.mVisible = true;

					// Record the start time the first time the scene plays
					// so we know when to fade in the music
					if ( sInstance.mStartTime <= 0.0f ) {
						sInstance.mStartTime = Time.realtimeSinceStartup;
					}
				}
			}

			private void Awake ()
			{
				sInstance = this;

				// Record original scale
				mScaleOrigin = transform.localScale;

				// Record the music volume, then set it to 0
				mAudioSource = GetComponent<AudioSource>();
				if ( mAudioSource != null ) {
					mVolume = mAudioSource.volume;
					mAudioSource.volume = 0.0f;
				}

				// Hide or show the scene, depending on the platform
#if UNITY_IOS && !UNITY_EDITOR
				Hide();
#else
				Show();
#endif
			}

			private void Update ()
			{
				// Fade in the background music
				if ( mAudioSource != null ) {
					mAudioSource.volume = mStartTime > 0.0f ? Mathf.Clamp( ( Time.realtimeSinceStartup - mStartTime ) / audioFadeDuration, 0.0f, 1.0f ) * mVolume : 0.0f;
				}

				// This value controls the interpolation of the scene
				// tracking so it won't appear to jump around
				const float speed = 0.5f;

				// Align the scene with the plane anchor values set by ARController
				transform.position = Vector3.Lerp( transform.position, mPosition, speed );
				transform.localRotation = Quaternion.Slerp( transform.localRotation, mRotation, speed );
				Vector3 s = mScale;
				s.x *= mScaleOrigin.x;
				s.y *= mScaleOrigin.y;
				s.z *= mScaleOrigin.z;
				transform.localScale = Vector3.Lerp( transform.localScale, s, speed );

				// Walk through animations and determine what needs to be removed
				if ( mAnimations != null ) {
					List<GameObject> completed = new List<GameObject>();
					foreach ( GameObject go in mAnimations ) {
						if ( go != null ) {
							Animator anim = go.GetComponent<Animator>();
							Firework firework = go.GetComponent<Firework>();
							Fuse fuse = go.GetComponent<Fuse>();
							if ( ( firework != null && firework.isComplete ) ||
								( fuse != null && fuse.isComplete ) ) {
								completed.Add( go );
							} else if ( anim != null ) {
								int i = anim.GetLayerIndex( "Play" );
								if ( i >= 0 ) {
									AnimatorStateInfo asi = anim.GetCurrentAnimatorStateInfo( i );
									if ( asi.normalizedTime >= 1.0f ) {
										completed.Add( go );
									}
								}
							}
						}
					}
					for ( int i = 0; i < completed.Count; i++ ) {
						GameObject go = completed[ i ];
						if ( go != null && mAnimations.Contains( go ) ) {
							go.transform.parent = null;
							mAnimations.Remove( go );
							Destroy( go );
						}
					}
				}
			}
		}
	}
}
