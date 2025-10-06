@echo off
del /f "d:\GitHub\ReMux2\ThumbExtractorWindow.xaml"
del /f "d:\GitHub\ReMux2\ThumbExtractorWindow.xaml.cs"
del /f "d:\GitHub\ReMux2\FrameExtractionService.cs"
del /f "d:\GitHub\ReMux2\yt_thumb.md"
del /f "d:\GitHub\ReMux2\RankerService.cs"
rmdir /s /q "d:\GitHub\ReMux2\haarcascades"
rmdir /s /q "d:\GitHub\ReMux2\haarcascades_cuda"
echo "Files and directories have been deleted."
pause