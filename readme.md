## BlackVue Downloader

BlackVue Downloader was made to facilitate getting images from your [BlackVue Dashcam](http://www.blackvue.com/dr650gw-2ch/) to a hard disk without having to touch the camera.

Without this program, you have to use the [mobile apps](http://www.blackvue.com/support/downloads/), [cloud service](http://www.blackvue.com/blackvue-over-the-cloud/), or you have to remove the SD Card from the camera, place it in a card reader, copy the files, eject the card, and put it back into the camera.  There's got to be a better way...

Fortunately, there is.  This [blog post](https://gadgetblogist.wordpress.com/2014/10/16/dashcam-hacking/) explains how the camera's Ad-Hoc WiFi and access via http work.  From this, it's easy to pull the files from the camera and into a folder.

Simply connect a Windows machine to the Ad-Hoc network, and run this program with the camera's IP address.  Once the files are downloaded, change the 'File path' of the BlackVue HD viewer to point to where the files are stored.

![BlackVue HD](Media/blackvue_hd.png)
