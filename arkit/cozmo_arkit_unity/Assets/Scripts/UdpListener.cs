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
		using System;
		using System.Net;
		using System.Net.Sockets;
		using System.Text;
		using System.Threading;

		public class UdpListener : MonoBehaviour
		{
			public int port = 8000;

			UdpClient mClient = null;
			IPEndPoint mEndPoint = null;
			Thread mThread = null;
			FireworkController.LaunchType mType = FireworkController.LaunchType.None;

			private void OnDestroy()
			{
				if ( mThread != null && mThread.IsAlive ) {
					mThread.Abort();
				}
			}

			private void Start()
			{
				mClient = new UdpClient( port );
				mEndPoint = new IPEndPoint( IPAddress.Any, 0 );

				mThread = new Thread( new ThreadStart( Listen ) );
				mThread.IsBackground = true;
				mThread.Start();
			}

			private void Listen()
			{
				while ( Thread.CurrentThread.IsAlive ) {
					try {
						if ( mClient != null && mEndPoint != null ) {
							byte[] data = mClient.Receive( ref mEndPoint );
							string text = Encoding.ASCII.GetString( data );

							int t = 0;
							if ( int.TryParse( text, out t ) ) {
								mType = (FireworkController.LaunchType)t;
							}
						}
					} catch ( Exception e ) {
						Debug.Log( e.ToString() );
					}
				}
			}

			private void Update()
			{
				if ( mType != FireworkController.LaunchType.None ) {
					FireworkController.Launch( mType );
					mType = FireworkController.LaunchType.None;
				}
			}
		}
	}
}
 