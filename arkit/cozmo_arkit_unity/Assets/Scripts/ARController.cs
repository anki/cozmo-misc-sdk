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
		using UnityEngine.XR.iOS;

		// Implements and requires the Unity ARKit plug-in 
		public class ARController : MonoBehaviour 
		{
			[Tooltip( "Prefab representing detected and tracked plane anchors" )]
			public GameObject planePrefab = null;

#if !UNITY_EDITOR
			// Singleton
			static private ARController sInstance = null;

			private int mIndex = 0;
			private Dictionary<string, ARPlaneAnchorGameObject> mPlaneAnchorMap = new Dictionary<string, ARPlaneAnchorGameObject>();

			// Handles new plane anchors
			public void ARAnchorAdded( ARPlaneAnchor anchorData )
			{
				GameObject go = UnityARUtility.CreatePlaneInScene( anchorData );
				if ( go != null ) {
					go.AddComponent<DontDestroyOnLoad>();
					ARPlaneAnchorGameObject arpag = new ARPlaneAnchorGameObject();
					arpag.planeAnchor.identifier = anchorData.identifier;
					arpag.planeAnchor = anchorData;
					arpag.gameObject = go;
					if ( mPlaneAnchorMap != null ) {
						mPlaneAnchorMap.Add( arpag.planeAnchor.identifier, arpag );
					}
					++mIndex;
				}
			}

			// Handles plane anchor updates
			public void ARAnchorUpdated( ARPlaneAnchor anchorData )
			{
				if ( mPlaneAnchorMap != null && mPlaneAnchorMap.ContainsKey( anchorData.identifier ) ) {
					ARPlaneAnchorGameObject arpag = mPlaneAnchorMap[ anchorData.identifier ];
					if ( arpag != null ) {
						UnityARUtility.UpdatePlaneWithAnchorTransform( arpag.gameObject, anchorData );
						arpag.planeAnchor = anchorData;
						mPlaneAnchorMap[ anchorData.identifier ] = arpag;
					}
				}
			}

			// Handles removed plane anchors
			public void ARAnchorRemoved( ARPlaneAnchor anchorData )
			{
				if ( mPlaneAnchorMap != null && mPlaneAnchorMap.ContainsKey( anchorData.identifier ) ) {
					ARPlaneAnchorGameObject arpag = mPlaneAnchorMap[ anchorData.identifier ];
					if ( arpag != null ) {
						GameObject.Destroy( arpag.gameObject );
						mPlaneAnchorMap.Remove( anchorData.identifier );
					}
				}
			}

			// Handles ARKit session failures
			public void ARSessionFailed( string error )
			{
				Debug.Log( "Session failed: " + error );	
			}

			private void Awake()
			{
				sInstance = this;
			}
#endif

			private void Start() 
			{
#if UNITY_EDITOR
				// Disable AR in editor
				if ( Camera.main != null ) {
					Camera.main.clearFlags = CameraClearFlags.Color;
					UnityARVideo arVideo = Camera.main.GetComponent<UnityARVideo>();
					if ( arVideo != null ) {
						arVideo.enabled = false;
					}
					Transform parent = Camera.main.transform.parent;
					if ( parent != null ) {
						UnityARCameraManager arCameraManager = parent.GetComponentInChildren<UnityARCameraManager>();
						if ( arCameraManager != null ) {
							arCameraManager.enabled = false;
						}
					}
				}
				AR3DOFCameraManager ar3dof = GetComponent<AR3DOFCameraManager>();
				if ( ar3dof != null ) {
					ar3dof.enabled = false;
				}
#else
				// Initialize the game object we'll use to 
				// represent plane anchors
				if ( planePrefab != null ) {
					UnityARUtility.InitializePlanePrefab( planePrefab );
				}

				// Add event listeners to the ARKit session
				UnityARSessionNativeInterface.ARAnchorAddedEvent += ARAnchorAdded;
				UnityARSessionNativeInterface.ARAnchorUpdatedEvent += ARAnchorUpdated;
				UnityARSessionNativeInterface.ARAnchorRemovedEvent += ARAnchorRemoved;
				UnityARSessionNativeInterface.ARSessionFailedEvent += ARSessionFailed;

				// Start the AR kit session
				ARKitWorldTackingSessionConfiguration sessionConfig = new ARKitWorldTackingSessionConfiguration( 
					UnityARAlignment.UnityARAlignmentCamera, 
					UnityARPlaneDetection.Horizontal
				);
				UnityARSessionRunOption sessionRunOptions = UnityARSessionRunOption.ARSessionRunOptionRemoveExistingAnchors | UnityARSessionRunOption.ARSessionRunOptionResetTracking;
				UnityARSessionNativeInterface.GetARSessionNativeInterface().RunWithConfigAndOptions( sessionConfig, sessionRunOptions );
#endif
			}


#if !UNITY_EDITOR
			private void OnDestroy()
			{
				// Remove event listeners
				UnityARSessionNativeInterface.ARAnchorAddedEvent -= ARAnchorAdded;
				UnityARSessionNativeInterface.ARAnchorUpdatedEvent -= ARAnchorUpdated;
				UnityARSessionNativeInterface.ARAnchorRemovedEvent -= ARAnchorRemoved;
				UnityARSessionNativeInterface.ARSessionFailedEvent -= ARSessionFailed;
			}

			private void Update()
			{
				if ( mPlaneAnchorMap != null ) {

					// This routine maps the scene to plane anchor, centering 
					// it around a touch point
					if ( UnityARSessionNativeInterface.GetARSessionNativeInterface() != null && Input.touchCount > 0 ) {

						// Get the screen location of the touch
						Touch touch = Input.GetTouch( 0 );
						if ( touch.phase == TouchPhase.Ended && Camera.main != null ) {
							Vector3 screenPosition = Camera.main.ScreenToViewportPoint( touch.position );
							ARPoint point = new ARPoint {
								x = screenPosition.x,
								y = screenPosition.y
							};

							// Hit test the plane anchor with the touch point
							List<ARHitTestResult> hitResults = UnityARSessionNativeInterface.GetARSessionNativeInterface().HitTest( point, ARHitTestResultType.ARHitTestResultTypeExistingPlane );
							if ( hitResults != null ) {
								foreach ( ARHitTestResult i in hitResults ) {

									// We have a hit
									if ( i.isValid ) {

										// Position the AR scene on the plane anchor where 
										// the touch point projects into it
										Vector3 p = UnityARMatrixOps.GetPosition( i.worldTransform );
										ARScene.SetPosition( p );

										// If this is the first time setting up the scene, we'll also 
										// rotate and scale it to fit the orientation and size of 
										// the plane anchor. Subsequent touches will only move
										// the scene for easy re-centering.
										if ( !ARScene.isVisible ) {
											Quaternion r = UnityARMatrixOps.GetRotation( i.worldTransform );
											r *= UnityARMatrixOps.GetRotation( i.localTransform );
											ARScene.SetRotation( r );
											string id = i.anchorIdentifier;
											if ( id.Length > 0 && mPlaneAnchorMap.ContainsKey( id ) ) {
												ARPlaneAnchorGameObject arpag = mPlaneAnchorMap[ id ];
												if ( arpag != null ) {
													Vector3 s = arpag.planeAnchor.extent;
													float d = s.x > s.z ? s.z : s.x;
													s = new Vector3( d, d, d );
													ARScene.SetScale( s );
												}
											}
										}

										// Show the scene
										ARScene.Show();
										break;
									}
								}
							}
						}
					}

					// Hide anchors when scene is visible
					foreach ( KeyValuePair<string, ARPlaneAnchorGameObject> kvp in mPlaneAnchorMap ) {
						ARPlaneAnchorGameObject arpag = kvp.Value;
						if ( arpag != null && arpag.gameObject != null ) {
							foreach ( Renderer r in arpag.gameObject.GetComponentsInChildren<Renderer>() ) {
								r.enabled = !ARScene.isVisible;
							}
						}
					}
				}
			}
#endif
		}
	}
}
