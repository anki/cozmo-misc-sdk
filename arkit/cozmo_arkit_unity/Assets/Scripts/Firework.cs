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

		[RequireComponent( typeof( AudioSource ) )]
		public class Firework : MonoBehaviour 
		{
			private enum ExplosionType : int
			{
				None, 
				Radial, 
				Spherical,
				Large,
				Count
			}

			// Returns true when this game object should be deleted
			public bool isComplete
			{
				get {
					return mComplete;
				}
			}

			// Explosion prefabs
			public GameObject radialExplosionPrefab = null;
			public GameObject largeExplosionPrefab = null;

			private AudioSource mAudioSource = null;
			private bool mComplete = false;
			private Vector3 mExplodePosition = Vector3.zero;
			private float mExplodeTime = 0.0f;
			private List<GameObject> mExplosions = new List<GameObject>();
			private Vector3 mNormal = Vector3.one.normalized;
			private GameObject mRocket = null;
			private float mRocketTime = 0.0f;
			private ExplosionType mType = ExplosionType.None;

			private GameObject CreateExplosion( Color c )
			{
				GameObject prefab = mType == ExplosionType.Large ? largeExplosionPrefab : radialExplosionPrefab;
				if ( prefab != null ) {
					GameObject go = Instantiate( prefab ) as GameObject;
					go.transform.parent = transform;
					foreach ( Renderer r in go.GetComponentsInChildren<Renderer>() ) {
						if ( r != null ) {
							Material m = r.material;
							if ( m != null ) {
								m.SetColor( "_Color", c );
							}
						}
					}
					Animator a = go.GetComponent<Animator>();
					if ( a != null ) {
						a.enabled = false;
					}
					go.SetActive( false );
					return go;
				}
				return null;
			}

			private void Start()
			{
				// Declare counter, color, and scale variables here so 
				// we can re-use them
				int i = 0;
				int count = 0;
				Color c = FireworkController.GetColor();
				Vector3 scale = Vector3.one;

				// Reference the explosion audio source
				mAudioSource = GetComponent<AudioSource>();

				// Pick firework type
				mType = (ExplosionType)( (int)Random.Range( 1.0f, (float)Firework.ExplosionType.Count ) );

				// Calculate rocket direction
				mNormal.x = Random.Range( -0.15f, 0.15f );
				mNormal.y = Random.Range( 0.85f, 1.0f );
				mNormal.z = Random.Range( -0.15f, 0.15f );
				mNormal.Normalize();

				// Find and set up the rocket
				if ( transform.childCount > 0 ) {
					Transform child = transform.GetChild( 0 );
					if ( child != null ) {
						mRocket = child.gameObject;
						if ( mRocket != null ) {
							mRocket.transform.localPosition = Vector3.zero;

							// Orient the rocket to its travel direction
							mRocket.transform.localRotation = Quaternion.Euler( mNormal * Mathf.Rad2Deg );

							// Randomly play screamer audio
							AudioSource[] a = mRocket.GetComponentsInChildren<AudioSource>();
							if ( a.Length > 1 ) {	
								for ( i = 0; i < a.Length; i++ ) {
									if ( a[ i ] != null && a[ i ].clip != null && ( i == 0 || Random.value > 0.8f ) ) {
										a[ i ].Play();
									}
								}
							}
						}
					}
				}

				// Choose a final destination for the rocket
				mExplodePosition = mNormal * Random.Range( 20.0f, 25.0f );

				// Populate a list of explosions to play after the rocket 
				// reaches its destination
				if ( mExplosions != null ) {
					switch ( mType ) {
					case ExplosionType.Radial:

						// Set up radial explosions
						count = Random.Range( 1, 4 );
						for ( i = 0; i < count; i++ ) {
							GameObject go = CreateExplosion( FireworkController.GetColor() );
							if ( go != null ) {
								go.transform.localPosition = mExplodePosition;
								go.transform.localRotation = Quaternion.Euler( Vector3.one * Random.value * 360.0f );
								go.transform.localScale = go.transform.localScale * FireworkController.RandomScale();

								mExplosions.Add( go );
							}
						}
						break;
					case ExplosionType.Spherical:

						// Use the single radial explosion to form a sphere
						count = Random.Range( 3, 7 );
						scale = Vector3.one * FireworkController.RandomScale();
						for ( i = 0; i < count; i++ ) {
							GameObject go = CreateExplosion( c );
							if ( go != null ) {
								go.transform.localPosition = mExplodePosition;
								go.transform.localRotation = Quaternion.Euler( Vector3.one * ( (float)i / (float)count ) * 360.0f );
								Vector3 s = go.transform.localScale;
								s.x *= scale.x;
								s.y *= scale.y;
								s.z *= scale.z;
								go.transform.localScale = s;

								mExplosions.Add( go );
							}
						}
						break;
					case ExplosionType.Large:
						{ // The large explosion animation
							GameObject go = CreateExplosion( c );
							if ( go != null ) {
								go.transform.localPosition = mExplodePosition;
								Vector3 s = go.transform.localScale;
								scale = Vector3.one * FireworkController.RandomScale();
								s.x *= scale.x;
								s.y *= scale.y;
								s.z *= scale.z;
								go.transform.localScale = s;

								mExplosions.Add( go );
							}
						}

						break;
					}
				}

				// Mark the time the rocket started
				mRocketTime = Time.realtimeSinceStartup;
			}

			private void Update()
			{
				float e = Time.realtimeSinceStartup;
				if ( mRocket != null ) {

					// Use time and easing to plot the rocket's position
					float t = ( e - mRocketTime ) / FireworkController.GetRocketDuration();
					t -= 1.0f;
					t = Mathf.Pow( t, FireworkController.GetRocketEasing() ) + 1.0f;
					mRocket.transform.localPosition = mExplodePosition * t;

					// Rocket animation is complete
					if ( t >= 1.0f ) {

						// Play the explosion audio and destroy the rocket
						Destroy( mRocket );
						mRocket = null;
						if ( mAudioSource != null ) {
							mAudioSource.clip = FireworkController.GetExplosion();
							if ( mAudioSource.clip != null ) {
								mAudioSource.Play();
							}
						}

						// Mark the explode time so we can clean up 
						// the scene in a bit
						mExplodeTime = e;
					}
				}

				// 'Sploded
				if ( mExplodeTime > 0.0f ) {

					// Play the explosions
					if ( mExplosions != null ) {
						foreach ( GameObject go in mExplosions ) {
							if ( go != null && !go.activeInHierarchy ) {
								Animator a = go.GetComponent<Animator>();
								if ( a != null ) {
									a.enabled = true;
								}
								go.SetActive( true );
							}
						}
					}

					// Mark this as complete after the appropriate delay
					// to tell ARScene to destroy this game object
					if ( e - mExplodeTime > ( mType == ExplosionType.Large ? 3.0f : 1.0f ) ) {
						mComplete = true;
					}
				}
			}
		}
	}
}
