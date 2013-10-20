# TwitterWinRT


Twitter API in Async/Await for WinRT

## Usage

1. Create an instance of TwitterWinRT with your consumerKey and consumerSecret for your App (the app need to have the right to read AND write tweets!!)

2. Check the AccessGranted property to detect if the user is already logued

3. If not, call `GainAccessToTwitter()`

4. You can call:

	  a. `GetUserTimeline()`
	
	  b. `GetTimeline()`
	
	  c. `UpdateStatus(String status)`
	  
	  d. `GetProfil(void || int userId || string userName)`
	  
	  e. ` Logout()`


[![Bitdeli Badge](https://d2weczhvl823v0.cloudfront.net/stumpyfr/twitterwinrt/trend.png)](https://bitdeli.com/free "Bitdeli Badge")

