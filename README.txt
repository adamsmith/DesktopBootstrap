I've written multiple desktop apps from scratch.  There's a lot of repeated work that goes into doing it well.  This is my attempt at a basic framework that will allow any project to get a running start.

It's built around a few assumptions and technology choices, which should be suitable for most projects.  Most code is written in C#, targeted against version 2 of the .NET framework.  We assume all users will have at least Windows XP.  The installer is written using NSIS.  We assume that updates should be done in the background, with no UI or UAC elevation prompts.

Here is a breakdown of these technology choices.  You are free to change them to suit your application, of course.  Afterwards is a list of instructions for getting started with your particular project.


[[The Basics]]

Basic architecture
* There are two main executables: DesktopBootstrap, and DesktopBootstrapService.
* The first is the main client app, which runs as the logged on user, and sits in the Windows system tray.
* The Service is a Windows service, which runs as SYSTEM, and is available to apply automatic updates, or do other things as SYSTEM.


Automatic updates
* Users love software that gets better over time, with no effort on their part.
* The auto updates mechanism should also be simple, and redundant.
* Most updates are applied by the Windows service, running as SYSTEM, so the user doesn't run into any UAC elevation prompts.
* If you break the Windows service auto updates, the client app also runs update check code.
* The update system assumes that any time is a good time for an updates, which you'll need to modify if there's some user interaction that shouldn't be interrupted.
* All update commands are signed using public key cryptography.
* The updates are downloaded over plain HTTP, but their hashes are checked before execution.
* The update public key is hard coded into the client.  The root CA system (and all of its flaws) are bypassed entirely.
* There's a python script included (make_new.py) that helps you generate your PROD and TEST update key pairs.
* The updater is based on an NSIS script, but could be any executable you generate.
* It is responsible for killing the client app, and/or stopping the Windows service.
* It then restarts the service, but should not restart the client app, since it's running as SYSTEM.
* The service then impersonates any users as appropriate to re-launch the client app.


Logging
* There is logging that's active at all times, and logging that conditionally activates if certain registry values are present.
* To activate the second kind of logging, run DebugFlags.reg.
* After doing so, all NSIS scripts (the installer, uninstaller, and updater) will OutputDebugString() various log messages.
* Use DbgView.exe or a similar app to read, log, and filter them to your heart's content!
* Those registry flags also turn on OutputDebugString() for client and service logging.
* Otherwise, on all installs, log files are written and rotated using NLog.
* See Log.cs for more!


ClientGuid's
* Each install is given a "ClientGuid" which is kind of like a cookie that lives in the registry.
* It's fairly brittle, but is a basic 80/20 for being able to identify users across web requests to your various web services.
* If you need anything more robust, I'd recommend a user account system.


Other keys
* The build system assumes you'll put your Authenticode keys in the client/Deployment/AuthenticodeKey folder.
* I'd recommend this for anything other than a quick hobby app.
* You should generate your own assembly strong signing keys as well, using sn, and put them in the client/ folder.


[[Getting Started]]

Training wheels
* First, download the project to your machine.  cd into that directory.
* Run python customize.py YourProjectName
* YourProjectName should not include any spaces.
* This will rename everything that says DesktopBootstrap to use YourProjectName.


Your first bicycle
* cd into YourProjectName/client
* sn -k YourProjectNameStrongSignKeys.snk
* (You may have to locate sn if it's not in your PATH.  It should be in your Visual Studio bin folder.)
* cd up one level, then into tools/Keys/self_signed
* rename yourprojectname_client_update_key yourprojectname_client_update_key_BACKUP (This folder is mostly there to give a flavor of what the keys look like.  We won't need them.)
* Before you run the make_new python script, you'll need OpenSSL in your PATH.  You can test this by running "openssl version"
* python make_new.py yourprojectname_client_update_key
* This script creates two update key pairs: PROD, and TEST.  PROD is generated first, followed by TEST.  (Note the line saying "NOW DOING TYPE PROD".)
* For PROD, you'll need to pick a passphrase used to encrypt your private key.
* Let's assume it's "tutu".
* The script will ask you to provide your passphrase several times.
* I counted four tutu's, followed by the certificate parameters (which really don't matter), followed by the challenge password (must be blank), followed by two tutu's, followed by the name of you p12 file ("YourProjectName PROD" the first time around, then "YourProjectName TEST" the second time around), followed by two more tutu's.
* For your TEST key pair, I'd recomment "test" as your passphrase.
* If you weren't comfortable doing this the first time around, just blast away the key directory and run it again!
* I'm sorry about all the run around in the script.  There are so many commands because we need each key in several representations to make all the moving pieces work together.  God help you if you want to use one of these key pairs in Java.  (Email me for Java Key Storeinstructions!)
* Now, securely delete all of the files in the new folder hierarchy that say delete_me.
* And finally, copy and paste the certificate.pem file contents of each of PROD and TEST into the relevant sections at the bottom of YourProjectName/client/SharedSource/UpdateChecker.cs.


Your first build
* Ensure that an instance of Visual Studio is running on your machine.  (This is needed for Dotfuscator.)
* cd into YourProjectName/client/Deployment/Installer
* nant
* Visual Studio will build your solution.
* Then you'll see Dotfuscator pop up.  Wait for the splash screen to go away, hit the play button on the toolbar, wait for obfuscation to finish, then close Dotfuscator.  You'll see nant continue its thing.
* Builds end up in the "builds" directory.


Your first update
* Change something small about the app.  Then we'll build and publish an update.
* Assuming you're still in the Installer directory, run "nant" as before.  This will produce build 2.
* Note that the updater is designed to be ran by the service, as SYSTEM, in session 0.
* So while you can double click it, its behavior will be a little off from ideal.
* Instead, put it up on a web server somewhere.  Let's suppose it's available from http://yourprojectname.com/YourProjectNameUpdater2.exe.
* You need some way to tell the client about the update.  This is done through an XML file that lists the executable location, and hash.
* This XML file is signed, using the update key pair we just generated.
* To generate this XML file, launch YourProjectNameUpdateSigner from Visual Studio.
* The app tries to guess at the values for the updater (latest build), the download URL, private key, etc.
* Enter the password for your PROD update key.  ("tutu" from above.)
* Click Go
* Assume it says Done!, you can close it now.  It created a file called YourProjectNameUpdateInfo.xml in the latest build directory.
* Now the client expects to get that XML back when it requests http://updates.yourprojectname.com/updateCheck?...
* To change that URL, edit YourProjectName/client/SharedSource/UpdateChecker.cs.
* On your test machine, I'd recommend changing your Windows hosts file to redirect updates.yourprojectname.com to 127.0.0.1, and then use YourProjectName/tools/FakeUpdateServer to serve up the XML.  Let's walk through that.
* First, copy FakeUpdateServer.exe and the update info XML file to your test machine's desktop.
* Start FakeUpdateServer.exe as an administrator, so it can listen on port 80.
* Make sure the XML file path points to the update XML file you copied over.
* You can ignore the exe path part.  It can be used to serve the updater executable, but we put it on a public Internet server, which is in some ways simpler.
* Make sure the Updates via Service checkbox is checked, and other is not.  This says to give the Windows service the update command, rather than the client app.
* Add a string registry value under HKEY_LOCAL_MACHINE\Software\Wow6432Node\YourProjectName, named ImmediateUpdateCheck, and set its value to "True".
* While you're at it, add the DebugFlags.reg values and open DbgView.exe, so you can see the update happen.  It's pretty fun to watch!
* Now, using the Windows Services tool, restart the YourProjectNameService service.
* The logging is pretty verbose.  Ten seconds after you restart the service, it will check for an update.  The FakeUpdateServer will send the update command XML.  The service will download the updater, verify its hash, and run it.  The updater will shut down the service and client app, update all files, and relaunch the service.  The service will relaunch the client app.  The whole process will take 
* You can verify that the update happened by right clicking on YourProjectName.exe in Program Files, going to Properties, then the Details tab.  The version should now be 1.0.0.2!


Accessorize
* You can edit the EULA, which is shown during the installer, by replacing YourProjectName/client/Deployment/Installer/License.rtf.
* You can edit your application's icon by replacing YourProjectName/tools/Artwork/icon/app.ico.
* But you'll also have to go into Visual Studio, and replace the parallel copies in each project's Properties, and in TrayIconForm's trayIcon.



Enjoy!