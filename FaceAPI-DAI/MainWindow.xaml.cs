using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using Newtonsoft.Json;
using System.IO;
using System.Globalization;
using System.Diagnostics;


namespace FaceAPIDAI
{
    /// <summary>
    /// FaceAPI-DAI!
    ///
    /// Created by Pieterbas Nagengast (AzureITis.nl) as a non-developer ;)
    /// @June 2017
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            GetFacegroupInfo();

        }

        /// <summary>
        /// Please update the Settings.settings file accordingly.
        /// Provide both Face API key and Face API URL (Azure region specific):
        /// 
        /// West US - https://westus.api.cognitive.microsoft.com/face/v1.0
        /// East US 2 - https://eastus2.api.cognitive.microsoft.com/face/v1.0
        /// West Central US - https://westcentralus.api.cognitive.microsoft.com/face/v1.0
        /// West Europe - https://westeurope.api.cognitive.microsoft.com/face/v1.0
        /// Southeast Asia - https://southeastasia.api.cognitive.microsoft.com/face/v1.0
        /// </summary>


        private readonly IFaceServiceClient faceServiceClient = new FaceServiceClient(Properties.Settings.Default.Key,Properties.Settings.Default.URL );
                      

        // Detect Faces and get Face Attributes
        private async Task<Face[]> UploadAndGetFaceAttribs(string imageFilePath)
        {
            try
            {
                using (Stream imageFileStream = File.OpenRead(imageFilePath))
                {
                    var requiredFaceAttributes = new FaceAttributeType[] {
                        FaceAttributeType.Age,
                        FaceAttributeType.Gender,
                        FaceAttributeType.Smile,
                        FaceAttributeType.FacialHair,
                        FaceAttributeType.HeadPose,
                        FaceAttributeType.Glasses
                    };

                    var faces = await faceServiceClient.DetectAsync(imageFileStream,true,true, returnFaceAttributes: requiredFaceAttributes);


                    return faces.ToArray();

                }
            }
            catch (Exception)
            {
                return new Face[0];
            }
        }
        
        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {

            //Upload image to be detected and indentified by Face API

            var openDlg = new Microsoft.Win32.OpenFileDialog();

            openDlg.Filter = "JPEG Image(*.jpg)|*.jpg";
            bool? result = openDlg.ShowDialog(this);

            if (!(bool)result)
            {
                return;
            }

            string filePath = openDlg.FileName;

            Uri fileUri = new Uri(filePath);
            BitmapImage bitmapSource = new BitmapImage();

            bitmapSource.BeginInit();
            bitmapSource.CacheOption = BitmapCacheOption.None;
            bitmapSource.UriSource = fileUri;
            bitmapSource.EndInit();

            FacePhoto.Source = bitmapSource;

            Title = "Detecting...";
            Face[] faces = await UploadAndGetFaceAttribs(filePath);


            Title = String.Format("Detection Finished. {0} face(s) detected", faces.Length);
            
            //add rectangle        

            if (faces.Length > 0)
            {
                DrawingVisual visual = new DrawingVisual();
                DrawingContext drawingContext = visual.RenderOpen();
                drawingContext.DrawImage(bitmapSource,
                    new Rect(0, 0, bitmapSource.Width, bitmapSource.Height));
                double dpi = bitmapSource.DpiX;
                double resizeFactor = 96 / dpi;
                
                foreach (var face in faces)
                {
                    //draw rectangle
                    drawingContext.DrawRectangle(
                    Brushes.Transparent,
                    new Pen(Brushes.AliceBlue, 1),
                    new Rect(
                        face.FaceRectangle.Left * resizeFactor,
                        face.FaceRectangle.Top * resizeFactor,
                        face.FaceRectangle.Width * resizeFactor,
                        face.FaceRectangle.Height * resizeFactor
                        )
                        );

                    

                    Guid[] faceID = new Guid[1];
                    faceID[0] = face.FaceId;
                                        
                    ComboBoxItem SelectedGrp = ComboBox1.SelectedItem as ComboBoxItem;

                    try
                    {
                        // Indentify Face(s)   
                        var results = await faceServiceClient.IdentifyAsync(SelectedGrp.ToolTip.ToString(), faceID);


                        if (results[0].Candidates.Length == 0)
                        {


                        }
                        else
                        {
                            // Get top 1 among all candidates returned
                            var candidateId = results[0].Candidates[0].PersonId;
                            var person = await faceServiceClient.GetPersonAsync(SelectedGrp.ToolTip.ToString(), candidateId);

                            
                            // Add persons name to rectangle
                            string fTxt = face.FaceAttributes.Gender +
                                Environment.NewLine +
                                "Age: " + face.FaceAttributes.Age +
                                Environment.NewLine + person.Name;
                                FormattedText ft = new FormattedText(fTxt,
                                CultureInfo.CurrentCulture,
                                FlowDirection.LeftToRight,
                                new Typeface(new FontFamily("Segoe"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                                18,    // 36 pt type
                                Brushes.AliceBlue);

                                // Draw the text at a location
                                drawingContext.DrawText(
                                ft,
                                new Point(face.FaceRectangle.Left, (face.FaceRectangle.Top + face.FaceRectangle.Height)));

                        }

                    }
                    catch (Exception)
                    {
                        // Add text to rectangle if person is unkown (unindentified)
                        string fTxt = face.FaceAttributes.Gender +
                            Environment.NewLine +
                            "Age: " + face.FaceAttributes.Age +
                            Environment.NewLine + "<unknown>";
                            FormattedText ft = new FormattedText(fTxt,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            new Typeface(new FontFamily("Segoe"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                            18,    // 36 pt type
                            Brushes.AliceBlue);

                            // Draw the text at a location
                            drawingContext.DrawText(
                            ft,
                            new Point(face.FaceRectangle.Left, (face.FaceRectangle.Top + face.FaceRectangle.Height)));
                    }
                }

                drawingContext.Close();
                RenderTargetBitmap faceWithRectBitmap = new RenderTargetBitmap(
                    (int)(bitmapSource.PixelWidth * resizeFactor),
                    (int)(bitmapSource.PixelHeight * resizeFactor),
                    96,
                    96,
                    PixelFormats.Pbgra32);

                faceWithRectBitmap.Render(visual);
                FacePhoto.Source = faceWithRectBitmap;
            }


        }
        
        private async void GetFacegroupInfo()
        {
            
            //Get PersonGroup, person and faces registered in Face API and put them in a TreeView
            PersonGroup[] personGroups = await faceServiceClient.ListPersonGroupsAsync();
                        
            foreach (var personGroup in personGroups)
            {
                TreeViewItem FaceGroupsItem = new TreeViewItem();
                
                FaceGroupsItem.Header = personGroup.Name.ToString();
                FaceGroupsItem.ToolTip = personGroup.PersonGroupId.ToString();
                FaceGroupsItem.ExpandSubtree();
                
                TreeRoot.Items.Add(FaceGroupsItem);

                ComboBoxItem _comboBoxItem = new ComboBoxItem();
                _comboBoxItem.Content = personGroup.Name.ToString();
                _comboBoxItem.ToolTip = personGroup.PersonGroupId.ToString();

                ComboBox1.Items.Add(_comboBoxItem);
                ComboBox1.SelectedIndex = 1;
                

                Person[] persons = await faceServiceClient.ListPersonsAsync(personGroup.PersonGroupId);
                
                    foreach (var person in persons)
                    {
                        TreeViewItem PersonItem = new TreeViewItem();
                        

                        PersonItem.Header = person.Name.ToString();
                        PersonItem.ToolTip = person.PersonId.ToString();
                        
                        FaceGroupsItem.Items.Add(PersonItem);

                    
                    
                    foreach (var faceID in person.PersistedFaceIds)
                        {
                            TreeViewItem FaceItem = new TreeViewItem();
                            FaceItem.Header = faceID.ToString();
                            FaceItem.ToolTip = faceID.ToString();
                            PersonItem.Items.Add(FaceItem);
                    }
                                
                }
                
            }

        }


        private  void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {
            //Dekete Person incl. Faces
            TreeViewItem SelectedPerson = TreeView1.SelectedItem as TreeViewItem;
            TreeViewItem ParentOfSelectedPerson = SelectedPerson.Parent as TreeViewItem;
            Guid PersonIDguid = new Guid(SelectedPerson.ToolTip.ToString());

            try
            {

                TreeViewItem SelectedGroup = TreeView1.SelectedItem as TreeViewItem;
                TreeViewItem ParentOfSelectedGroup = SelectedGroup.Parent as TreeViewItem;

                var DeletePerson = faceServiceClient.DeletePersonAsync(ParentOfSelectedPerson.ToolTip.ToString(), PersonIDguid);

                ParentOfSelectedGroup.Items.Remove(SelectedGroup);
                TreeView1.Items.Refresh();
                TreeView1.UpdateLayout();

            }
            catch (Exception)
            {

            }

  
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            //add details to create person group           
            string msgText = "Add group name and id";
            string msglbl1Text = "Group Name:";
            string msglbl2Text = "Group ID:";

            InputBoxText.Text = msgText;
            InputLabel1.Content = msglbl1Text;
            InputLabel2.Content = msglbl2Text;
            PersonOkButton.Visibility = System.Windows.Visibility.Hidden;
            InputBox.Visibility = System.Windows.Visibility.Visible;
            
            
        }

        private void CanelButton_Click(object sender, RoutedEventArgs e)
        {
            InputBox.Visibility = System.Windows.Visibility.Collapsed;
            InputTextBox1.Text = string.Empty;
            InputTextBox2.Text = string.Empty;
        }

   

        private void GroupOkButton_Click(object sender, RoutedEventArgs e)
        {

            //Create person group
            try
            {
                TreeViewItem SelectedGroup = TreeView1.SelectedItem as TreeViewItem;
               
                var AddGroup = faceServiceClient.CreatePersonGroupAsync(InputTextBox2.Text, InputTextBox1.Text);

                TreeViewItem FaceGroupsItem = new TreeViewItem();

                FaceGroupsItem.Header = InputTextBox1.Text;
                FaceGroupsItem.ToolTip = InputTextBox2.Text;

                SelectedGroup.Items.Add(FaceGroupsItem);
                ComboBox1.Items.Add(InputTextBox1.Text);
                
                TreeView1.Items.Refresh();
                TreeView1.UpdateLayout();


            }
            catch (Exception)
            {
            
            }


            InputBox.Visibility = System.Windows.Visibility.Collapsed;
            InputTextBox1.Text = string.Empty;
            InputTextBox2.Text = string.Empty;
        }

        private async void PersonOkButton_Click(object sender, RoutedEventArgs e)
        {
            //Create Person
            try
            {
                TreeViewItem SelectedGroup = TreeView1.SelectedItem as TreeViewItem;
                var CreatePerson = await faceServiceClient.CreatePersonAsync(SelectedGroup.ToolTip.ToString(), InputTextBox1.Text);

                TreeViewItem PersonItem = new TreeViewItem();

                PersonItem.Header = InputTextBox1.Text;
                PersonItem.ToolTip = CreatePerson.PersonId.ToString();

                SelectedGroup.Items.Add(PersonItem);
                TreeView1.Items.Refresh();
                TreeView1.UpdateLayout();


            }
            catch (Exception)
            {

            }
            InputBox.Visibility = System.Windows.Visibility.Collapsed;
            InputTextBox1.Text = string.Empty;
            InputTextBox2.Text = string.Empty;
        }

        private void MenuItem_Click_2(object sender, RoutedEventArgs e)
        {
                 
            //Delete personGroup
            try
            {
                TreeViewItem SelectedGroup = TreeView1.SelectedItem as TreeViewItem;
                TreeViewItem ParentOfSelectedGroup = SelectedGroup.Parent as TreeViewItem;
                var DeleteGroup = faceServiceClient.DeletePersonGroupAsync(SelectedGroup.ToolTip.ToString());

                ComboBox1.Items.Remove(SelectedGroup.Header.ToString());
                ParentOfSelectedGroup.Items.Remove(SelectedGroup);
                TreeView1.Items.Refresh();
                TreeView1.UpdateLayout();




            }
            catch (Exception)
            {

            }
        }

        private void MenuItem_Click_3(object sender, RoutedEventArgs e)
        {
            // add person
            string msgText = "Add Persons Name";
            string msglbl1Text = "Name:";
            
            InputBoxText.Text = msgText;
            InputLabel1.Content = msglbl1Text;

            InputTextBox1.Visibility = System.Windows.Visibility.Visible;
            InputTextBox2.Visibility = System.Windows.Visibility.Hidden;
            InputLabel2.Visibility = System.Windows.Visibility.Hidden;
            GroupOkButton.Visibility = System.Windows.Visibility.Hidden;
            PersonOkButton.Visibility = System.Windows.Visibility.Visible;
            InputBox.Visibility = System.Windows.Visibility.Visible;
            
        }

        private  void MenuItem_Click_4(object sender, RoutedEventArgs e)
        {
            //Train Person Gorup
            TreeViewItem SelectedGroup = TreeView1.SelectedItem as TreeViewItem;

            try
            {
                var TrainGroup =  faceServiceClient.TrainPersonGroupAsync(SelectedGroup.ToolTip.ToString());

            }
            catch (Exception)
            {

            }
        }

        private async void MenuItem_Click_5(object sender, RoutedEventArgs e)
        {

            //Add Face to person
            TreeViewItem SelectedPerson = TreeView1.SelectedItem as TreeViewItem;
            TreeViewItem ParentOfSelectedPerson = SelectedPerson.Parent as TreeViewItem;
            Guid PersonIDguid = new Guid(SelectedPerson.ToolTip.ToString());
                
            var openDlg = new Microsoft.Win32.OpenFileDialog();

            openDlg.Filter = "JPEG Image(*.jpg)|*.jpg";
            bool? result = openDlg.ShowDialog(this);

            if (!(bool)result)
            {
                return;
            }

            string filePath = openDlg.FileName;

            try
            {
                Stream s = File.OpenRead(filePath);
                
                var AddFace = await faceServiceClient.AddPersonFaceAsync(ParentOfSelectedPerson.ToolTip.ToString(), PersonIDguid, s);

                TreeViewItem FaceItem = new TreeViewItem();

                FaceItem.Header = AddFace.PersistedFaceId.ToString();
                FaceItem.ToolTip = AddFace.PersistedFaceId.ToString();

                SelectedPerson.Items.Add(FaceItem);
                TreeView1.Items.Refresh();
                TreeView1.UpdateLayout();

            }
            catch (Exception)
            {

            }

        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void TreeView1_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            TreeViewItem Item = sender as TreeViewItem; if (Item != null) { Item.IsSelected = true; }
        }
    }


}
