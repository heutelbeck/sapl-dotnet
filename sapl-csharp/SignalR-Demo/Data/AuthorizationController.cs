/*
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *      http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SignalR_Demo.Hubs;

namespace SignalR_Demo.Data
{
    public class AuthorizationController : Controller
    {
        private readonly IHubContext<ChatHub> _hubContext;

        public AuthorizationController(IHubContext<ChatHub> hubContext)
        {
            _hubContext = hubContext;
        }
    }
}
