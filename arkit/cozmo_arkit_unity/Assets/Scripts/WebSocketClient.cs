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
//
namespace Cozmo
{
	namespace AR
	{
		using UnityEngine;

		// Requires and implements Jonathan Pavlou's WebSocketUnity.
		// Open "/Assets/WebSocketUnity/README.md" for more information.
		public class WebSocketClient : MonoBehaviour, WebSocketUnityDelegate
		{
			[Tooltip( "URL of Cozmo's web socket server" )]
			public string url = "ws://echo.websocket.org";

			private bool mConnect = false;
			private WebSocketUnity mSocket = null;
			private string mUrl = "";

			private void OnDestroy()
			{
				// Close web socket on the way out
				if ( mSocket != null && mSocket.IsOpened() ) {
					mSocket.Close();
				}
			}

			private void Update()
			{
				if ( url != mUrl ) {
					if ( mSocket != null && mSocket.IsOpened() ) {
						mSocket.Close();
						mSocket = null;
						mConnect = false;
					} else {
						mConnect = true;
					}
				}
				mUrl = url;

				if ( mConnect ) {
					if ( mSocket == null ) {
						mSocket = new WebSocketUnity( url, this );
					}
					mSocket.Open();
					mConnect = false;
				}
			}

			// Implements WebSocketUnityDelegate
			public void OnWebSocketUnityOpen( string sender )
			{
				Debug.Log( sender + " opened" );
			}

			// Implements WebSocketUnityDelegate
			public void OnWebSocketUnityClose( string reason )
			{
				Debug.Log( "Socket closed: " + reason );
				mConnect = true;
			}

			// Implements WebSocketUnityDelegate
			public void OnWebSocketUnityReceiveMessage( string message )
			{
				Debug.Log( "Message received: " + message );

				// The server sends numbers representing values in 
				// the `FireworkController.LaunchType` enumerator
				int t = 0;
				if ( int.TryParse( message, out t ) ) {
					FireworkController.Launch( (FireworkController.LaunchType)t );
				}
			}

			// Implements WebSocketUnityDelegate
			public void OnWebSocketUnityReceiveDataOnMobile( string base64EncodedData )
			{
				Debug.Log( "Encoded data received: " + base64EncodedData );
			}

			// Implements WebSocketUnityDelegate
			public void OnWebSocketUnityReceiveData( byte[] data )
			{
				Debug.Log( "Data received: " + System.Text.Encoding.UTF8.GetString( data ) );
			}

			// Implements WebSocketUnityDelegate
			public void OnWebSocketUnityError( string error )
			{
				Debug.Log( "Socket error: " + error );
			}
		}
	}
}
