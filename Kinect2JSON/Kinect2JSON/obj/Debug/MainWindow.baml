   M S B A M L   `   `   `  ����  Kinect2JSON    Kinect2JSON.MainWindow   5   	   \ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35D 9http://schemas.microsoft.com/winfx/2006/xaml/presentation    5   	   R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5   	   -   5          � Title$  
MainWindow��5   	   $	��518��5	   	   $
��1230��6    � 	Resources 5         MediumGreyBrush �� Color  KinectPurpleBrush  KinectBlueBrush �� 
TargetType �� Property �� Value��%�   &	        &	 #     &	 F     '�� i     �� 5   
   $ 	#ff6e6e6e��62   �� 5   
   $ 	#ff52318f��64   �� 5   
   $ 	#ff00BCF2��62   �� 5   
   " ��6   .�5      ��  �@J�SnapsToDevicePixels . 64   5      5      .��5      � $��	10 0 10 0q�6    � RowDefinitions 5   
   �� 5      $
��Auto �6   �� 5      $	��400 �6   �� 5      $
��Auto �6   �� 5      $
��Auto �6    � ColumnDefinitions 5   
   �� 5      $
��Auto �6   �� 5      $
��Auto �6   .+�5    
   �� $��0��6   $��0��6   $��Images/Logo.png��6.   $
��Left=�6G   $
��Fill=�6b   $��32��6q   $��81��6}   $��0 10 0 5q�6�   �� 5!   
   $��0��6   $��0��6!   $��0 0 -1 0q�61   $��Right=�6C   $��Bottom=�6_   #��[  6z   $��Segoe UI)�6�   $��18(�6�   Kinect2JSON6�   �� 5"   
   $��0��6   $��1��6   $��Images/Status.png��6-   $
��None=�6H   $��Right=�6W   $��0 0 0 5q�6s   (� 5$   
   $��1��6   $��0��6   $��Center=�6/   .��5%      �� $��UniformToFill=�64   ��6   �� *ImageSource1+64   5&      (� 5'   
   $��1��6   $��1��6   $��Center=�6/   .��5(      �� $��UniformToFill=�64   ��6   �� *ImageSource2+64   5)      �� 5,   
   -   5        $��kinectStatusBar��5,   O   $��2��6   $��0��6!   $��Stretch=�61   $��Bottom=�6f   
�������#��[  6�   .@�5-      �� ��6   �� *KinectStatusText+5.      �� 50   
   -   5        $��publicConnectionStatusBar��50   O   $��2��6   $��1��6!   $��Stretch=�61   $��Bottom=�6p   
�������#��[  6�   .@�51      �� ��6   �� *PublicConnectionStatusText+52      53      55      