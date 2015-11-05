using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace INFOIBV
{
    public partial class INFOIBV : Form
    {
        private Bitmap InputImage;
        private Bitmap OutputImage;

        public INFOIBV()
        {
            InitializeComponent();
        }
        

        private void LoadImageButton_Click(object sender, EventArgs e)
        {
           if (openImageDialog.ShowDialog() == DialogResult.OK)             // Open File Dialog
            {
                string file = openImageDialog.FileName;                     // Get the file name
                imageFileName.Text = file;                                  // Show file name
                if (InputImage != null) InputImage.Dispose();               // Reset image
                InputImage = new Bitmap(file);                              // Create new Bitmap from file
                if (InputImage.Size.Height <= 0 || InputImage.Size.Width <= 0 ||
                    InputImage.Size.Height > 512 || InputImage.Size.Width > 512) // Dimension check
                    MessageBox.Show("Error in image dimensions (have to be > 0 and <= 512)");
                else
                    pictureBox1.Image = (Image) InputImage;                 // Display input image
            }
        }

        private void applyButton_Click(object sender, EventArgs e)
        {
            Thread thread = new Thread(new ThreadStart(DoIt));
            thread.Start();
            applyButton.Enabled = false;
        }

        private void DoIt()
        {
            if (InputImage == null) return;                                 // Get out if no input image
            if (OutputImage != null) OutputImage.Dispose();                 // Reset output image
            OutputImage = new Bitmap(InputImage.Size.Width, InputImage.Size.Height); // Create new output image
            Color[,] Image = new Color[InputImage.Size.Width, InputImage.Size.Height]; // Create array to speed-up operations (Bitmap functions are very slow)


            // Copy input Bitmap to array            
            for (int x = 0; x < InputImage.Size.Width; x++)
            {
                for (int y = 0; y < InputImage.Size.Height; y++)
                {
                    Image[x, y] = InputImage.GetPixel(x, y);                // Set pixel color in array at (x,y)
                }
            }

            //==========================================================================================
            // TODO: include here your own code
            // example: create a negative image
            Grayscale(InputImage, Image);
            Threshold(InputImage, Image, 90);
            SpecialErosion(InputImage, Image);
            // Closing om het aantal gaten te reduceren.
            Dilation(InputImage, Image, 3);
            Erosion(InputImage, Image, 3);
            ObjectVinden(InputImage, Image);
            List<LosObject> LosObjectList = FilterObjecten(InputImage,Image);
            //==========================================================================================
           
            //Vergelijk percentages van gevonden objecten en concludeer het merk van de auto.                      
            float HoogstePercentage = 0;
            string Merk = "undefined";
            Point FirstPoint= new Point(0,0);
            Parallel.ForEach(LosObjectList, losobject =>
            {
                if (losobject.AudiPercentage > HoogstePercentage)
                {
                    HoogstePercentage = losobject.AudiPercentage;
                    Merk = "Audi";
                    FirstPoint = losobject.FindWhitePixel();
                }
                if(losobject.VolksWagenPercentage > HoogstePercentage)
                {
                    HoogstePercentage = losobject.VolksWagenPercentage;
                    Merk = "VolksWagen";
                    FirstPoint = losobject.FindWhitePixel();
                }
                if(losobject.MazdaPercentage > HoogstePercentage)
                {
                    HoogstePercentage = losobject.MazdaPercentage;
                    Merk = "Mazda";
                    FirstPoint = losobject.FindWhitePixel();
                }
            });
            if (HoogstePercentage > 0.6)
            {
                System.Windows.Forms.MessageBox.Show("Het merk is: " + Merk + HoogstePercentage);
                int index = 0;
                List<Point> Tevullen = new List<Point>();
                int kleur;
                Point LogoPoint = FirstPoint;
                kleur = Image[LogoPoint.X, LogoPoint.Y].G;
                Tevullen.Add(LogoPoint);
                try
                {
                    while (Tevullen[index] != null)
                    {
                        ObjectMarkeren(InputImage, Image, Tevullen[index].X, Tevullen[index].Y, kleur, Tevullen, index);
                        index += 1;
                    }

                }
                catch
                {
                    //a fish
                }
            }
            else
                System.Windows.Forms.MessageBox.Show("Geen autologo gevonden." + LosObjectList.Count());  // Geen merk kunnen vinden.
            
            
            
            //==========================================================================================

            // Copy array to output Bitmap
            for (int x = 0; x < InputImage.Size.Width; x++)
            {
                for (int y = 0; y < InputImage.Size.Height; y++)
                {
                    OutputImage.SetPixel(x, y, Image[x, y]);               // Set the pixel color at coordinate (x,y)
                }
            }
            
            pictureBox2.Image = (Image)OutputImage;                         // Display output image

        }

        private void ObjectMarkeren(Bitmap InputImage, Color[,] Image, int x, int y, int kleur, List<Point> Tevullen, int index)
        {
            Image[x, y] = Color.Red;                              //Dit punt kleuren en de rest checken of ze gekleurd moeten worden en dan op de stack zetten.
            if (x > 0 && Image[x - 1, y] == Color.FromArgb(kleur,kleur,kleur) && !Tevullen.Contains(new Point(x - 1, y)))
                Tevullen.Add(new Point(x - 1, y));
            if (y > 0 && Image[x, y - 1] == Color.FromArgb(kleur, kleur, kleur) && !Tevullen.Contains(new Point(x, y - 1)))
                Tevullen.Add(new Point(x, y - 1));
            if (y < InputImage.Size.Height - 1 && Image[x, y + 1] == Color.FromArgb(kleur, kleur, kleur) && !Tevullen.Contains(new Point(x, y + 1)))
                Tevullen.Add(new Point(x, y + 1));
            if (x < InputImage.Size.Width - 1 && Image[x + 1, y] == Color.FromArgb(kleur, kleur, kleur) && !Tevullen.Contains(new Point(x + 1, y)))
                Tevullen.Add(new Point(x + 1, y));
        }


        private void NegativeImage(Bitmap InputImage, Color[,] Image)
        {
            for (int x = 0; x < InputImage.Size.Width; x++)
            {
                for (int y = 0; y < InputImage.Size.Height; y++)
                {
                    Color pixelColor = Image[x, y];                         // Get the pixel color at coordinate (x,y)
                    Color updatedColor = Color.FromArgb(255 - pixelColor.R, 255 - pixelColor.G, 255 - pixelColor.B); // Negative image
                    Image[x, y] = updatedColor;                             // Set the new pixel color at coordinate (x,y)
                }
            }
        }  

        private void Grayscale(Bitmap InputImage, Color[,] Image)
        {
            for (int x = 0; x < InputImage.Size.Width; x++)
            {
                for (int y = 0; y < InputImage.Size.Height; y++)
                {
                    Color pixelColor = Image[x, y];                         // Get the pixel color at coordinate (x,y)
                    Color updatedColor = Color.FromArgb((pixelColor.R + pixelColor.G + pixelColor.B) / 3, (pixelColor.R + pixelColor.G + pixelColor.B) / 3, (pixelColor.R + pixelColor.G + pixelColor.B) / 3); // Greyscale image
                    Image[x, y] = updatedColor;                             // Set the new pixel color at coordinate (x,y)
                }
            }
        }

        private void Threshold(Bitmap InputImage, Color[,] Image, int Value)
        {
            for (int x = 0; x < InputImage.Size.Width; x++)
            {
                for (int y = 0; y < InputImage.Size.Height; y++)                //Door de image heen loopen.
                {
                    Color pixelColor = Image[x, y];                         // Get the pixel color at coordinate (x,y).
                    Color updatedColor = Color.FromArgb(0, 0, 0);
                    if(pixelColor.R >= Value)
                    {
                        updatedColor = Color.FromArgb(255, 255, 255);               //Afhankelijk van de waarde wel of niet wit maken.
                    }

                    Image[x, y] = updatedColor;                             // Set the new pixel color at coordinate (x,y).
                }
            }
        }
        
        // De value waarde is de grootte van de kernel, zowel bij de erosion als diliation methode.
        private void Erosion(Bitmap InputImage, Color[,] Image, int Value)                      //Zie dilation, maar dan andersom.
        {
            Color[,] NewImage = new Color[InputImage.Size.Width, InputImage.Size.Height];

            for (int x = 0; x < InputImage.Size.Width; x++)
            {
                for (int y = 0; y < InputImage.Size.Height; y++)
                {                   
                    Color updatedColor = Image[x,y]; 
                    NewImage[x, y] = updatedColor;                                                      
                }
            }

            for (int x = (Value - 1) / 2; x < InputImage.Size.Width - (Value - 1) / 2; x++)
            {
                for (int y = (Value - 1) / 2; y < InputImage.Size.Height - (Value - 1) / 2; y++)
                {
                    bool fail = false;
                    for (int n = x - (Value -1)/2; n<= x+(Value -1)/2; n++)
                    {
                        for(int t = y - (Value-1)/2; t<=y+(Value-1)/2; t++)
                        {
                            if(NewImage[n,t] == Color.FromArgb(0, 0, 0))
                            {
                                fail = true;
                                break;
                            }
                        }
                        if (fail == true)
                            break;
                    }
                    if (fail == true)
                        Image[x, y] = Color.FromArgb(0, 0, 0);
                    else
                        Image[x, y] = Color.FromArgb(255, 255, 255);
                    
                }
            }
        }


        private void Dilation(Bitmap InputImage, Color[,] Image, int Value)
        {
            Color[,] NewImage = new Color[InputImage.Size.Width, InputImage.Size.Height];

            for (int x = 0; x < InputImage.Size.Width; x++)
            {
                for (int y = 0; y < InputImage.Size.Height; y++)
                {
                    Color updatedColor = Image[x, y];
                    NewImage[x, y] = updatedColor;                             //Kopie van de bitmap om dilation uit te voeren.                      
                }
            }

            for (int x = (Value - 1) / 2; x < InputImage.Size.Width - (Value - 1) / 2; x++)
            {
                for (int y = (Value - 1) / 2; y < InputImage.Size.Height - (Value - 1) / 2; y++)        //Door de image heen loopen.
                {
                    bool fail = false;
                    for (int n = x - (Value - 1) / 2; n <= x + (Value - 1) / 2; n++)            //Door de grote van de kernel heen loopen om te zien hoe we de pixel moeten kleuren,
                    {
                        for (int t = y - (Value - 1) / 2; t <= y + (Value - 1) / 2; t++)
                        {
                            if (NewImage[n, t] == Color.FromArgb(255, 255, 255))
                            {
                                fail = true;
                                break;
                            }
                        }
                        if (fail == true)
                            break;
                    }
                    if (fail == true)
                        Image[x, y] = Color.FromArgb(255, 255, 255);              //Pixel kleuren,
                    else
                        Image[x, y] = Color.FromArgb(0, 0, 0);

                }
            }
        }
        
        private void ObjectVinden(Bitmap InputImage, Color[,] Image)  // Floodfill algoritme, ieder object een eigen grijswaarde kleur geven.
        {
            int kleur = 1;                                          //De eerste grijswaarde.
            for (int x = 0; x < InputImage.Size.Width; x++)
            {
                for (int y = 0; y < InputImage.Size.Height; y++)
                {
                    if(Image[x,y] == Color.FromArgb(255,255,255))
                    {
                        int index = 0;
                        List<Point> Tevullen = new List<Point>();
                        Tevullen.Add(new Point(x, y));
                        try
                        {
                            while(Tevullen[index]!=null)
                            {
                            ObjectVullen(InputImage, Image, Tevullen[index].X, Tevullen[index].Y, kleur, Tevullen, index);
                            index += 1;
                            }
                        }
                        catch                       //Relatief goedkope manier om te kijken of de index out of bounds is en zodra dat zo is en we dus klaar zijn met vullen naar de volgende te gaan.
                        {
                            kleur += 1;
                            if (kleur == 0 || kleur == 255)     //Ook al staat dit hier we gaan er vanuit dat er geen 255 objecten in het plaatje zitten.
                                kleur = 1;
                        }
                    }
                }
            }
        }

        private void ObjectVullen(Bitmap InputImage, Color[,] Image, int x , int y, int kleur, List<Point> Tevullen, int index)
        {
                Image[x, y] = Color.FromArgb(kleur, kleur, kleur);                              //Dit punt kleuren en de rest checken of ze gekleurd moeten worden en dan op de stack zetten.
                if (x > 0 && Image[x - 1, y] == Color.FromArgb(255, 255, 255) && !Tevullen.Contains(new Point(x - 1, y)))
                    Tevullen.Add(new Point(x - 1, y));
                if (y > 0 && Image[x, y - 1] == Color.FromArgb(255, 255, 255) && !Tevullen.Contains(new Point(x, y - 1)))
                    Tevullen.Add(new Point(x, y - 1));
                if (y < InputImage.Size.Height-1 && Image[x, y + 1] == Color.FromArgb(255, 255, 255) && !Tevullen.Contains(new Point(x, y + 1)))
                    Tevullen.Add(new Point(x, y + 1));
                if (x < InputImage.Size.Width -1 && Image[x + 1, y] == Color.FromArgb(255, 255, 255) && !Tevullen.Contains(new Point(x + 1, y)))
                    Tevullen.Add(new Point(x + 1, y));
        }

        private List<LosObject> FilterObjecten(Bitmap InputImage, Color[,] Image)  // Objecten eruit filteren die sowieso geen autologo kunnen zijn (kijkend naar verhouding etc.)
        {
            List<LosObject> LosObjectenList = new List<LosObject>();
            for (int kleur = 1; kleur < 256;kleur++)
            {
                int hoogstex = -1, hoogstey = -1, laagstex = 513, laagstey = 513;
                for (int x = 0; x < InputImage.Size.Width; x++)
                {
                    for (int y = 0; y < InputImage.Size.Height; y++)
                    {
                        if (Image[x, y] == Color.FromArgb(kleur, kleur, kleur))
                        {
                            if (x > hoogstex)
                                hoogstex = x;
                            if (x < laagstex)
                                laagstex = x;
                            if (y < laagstey)
                                laagstey = y;
                            if (y > hoogstey)
                                hoogstey = y;
                        }
                    }
                }
                if (hoogstex == -1)
                {
                    break;
                }
                float Verhouding = ((float) (hoogstex - laagstex))/((float) (hoogstey-laagstey));
                if (Verhouding >0.9 && Verhouding < 3.5)
                    if ((hoogstex - laagstex) * (hoogstey - laagstey) > 400)     
                        LosObjectenList.Add(new LosObject(Image, laagstex, laagstey, hoogstex, hoogstey, kleur));
            }
            return LosObjectenList;
        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            if (OutputImage == null) return;                                // Get out if no output image
            if (saveImageDialog.ShowDialog() == DialogResult.OK)
                OutputImage.Save(saveImageDialog.FileName);                 // Save the output image
        }

        private void SpecialErosion(Bitmap InputImage, Color[,] Image)  // Speciale versie van erosion, omdat deze versie voor ieder autologo die we willen herkennen werkt. (in tegenstelling tot combinaties van de normale erosion en dilation)
        {
            Color[,] NewImage = new Color[InputImage.Size.Width, InputImage.Size.Height];

            for (int x = 0; x < InputImage.Size.Width; x++)
            {
                for (int y = 0; y < InputImage.Size.Height; y++)
                {
                    Color updatedColor = Image[x, y];
                    NewImage[x, y] = updatedColor;                             //Kopie van de bitmap om erosion uit te voeren                        
                }
            }

            for (int x = 1; x < InputImage.Size.Width-1; x++)
            {
                for (int y = 1; y < InputImage.Size.Height-1; y++)
                {
                    if(NewImage[x,y]==Color.FromArgb(255,255,255))
                    {
                        int AmountWhiteNeighbour = 0;
                        for (int n = x - 1; n <= x + 1; n++)            //Door de grote van de kernel heen loopen om te zien hoe we de pixel moeten kleuren
                        {
                            for (int t = y - 1; t <= y + 1; t++)
                            {
                                if (NewImage[n, t] == Color.FromArgb(255, 255, 255))
                                {
                                    AmountWhiteNeighbour += 1;
                                }
                            }
                        }
                        if (AmountWhiteNeighbour < 7)
                            Image[x,y] = Color.FromArgb(0,0,0);
                    }
                }
            }

        }

    }

    class LosObject
    {
        private int beginx;
        private int endx;
        private int beginy;
        private int endy;
        public float AudiPercentage, VolksWagenPercentage, MazdaPercentage;
        private Color[,] Object;
        private int amountOfHoles;
        private Point startPoint;

        public LosObject(Color[,] Image, int beginx, int beginy, int endx, int endy, int kleur)
        {
            Object = new Color[endx - beginx, endy - beginy];
            for (int x = beginx; x < endx; x++)
            {
                for (int y = beginy; y < endy; y++)
                {
                    if(Image[x,y] == Color.FromArgb(kleur, kleur, kleur))
                        Object[x - beginx, y - beginy] = Color.FromArgb(255, 255, 255);
                    else
                        Object[x - beginx, y - beginy] = Color.FromArgb(0, 0, 0);   //kopie van deel van orginele afbeelding wat alleen ons object bevat
                }
            }
            this.beginx = beginx;
            this.beginy = beginy;
            this.endx = endx;
            this.endy = endy;
            startPoint = new Point(beginx, (endy - beginy) / 2); 
            // Aantal holes bepalen.
            amountOfHoles = CountHoles();
            // Percentages bepalen.
            AudiPercentage = BekijkPercentageAudi();
            VolksWagenPercentage = BekijkPercentageVolksWagen();
            MazdaPercentage = BekijkPercentageMazda();
        }

        // Vinden van het aantal holes met behulp van een lijn in het midden van een object. Het aantal holes dat met deze lijn snijdt is meestal niet gelijk aan het aantal holes in het logo, maar meestal wel uniek.
        public int CountHoles()
        {
            int holes = 0;
            bool firstBlackPixel = true;

            for (int i = 0; i <= (endx - beginx) - 1; i++)
            {
                if (Object[i, startPoint.Y] != Color.FromArgb(0, 0, 0))
                    firstBlackPixel = true;
                if (Object[i, startPoint.Y] == Color.FromArgb(0,0,0) && firstBlackPixel == true)
                {
                    holes++;
                    firstBlackPixel = false;
                }
            }

            return holes;
        }

        // Percentage uitrekenen voor welk merk het kan zijn, kijkend naar de oppervlakte, verhouding en het aantal holes.
        // De area hebben we bij alle merken wat lager genomen dan dat het werkelijk is, omdat deze kleiner is geworden door de erosion.
        private float BekijkPercentageVolksWagen()
        {
            float Percentage = 1;
            Percentage *= Math.Max(0, 1 - (Math.Abs((float)0.5 - Oppervlakte())));
            Percentage *= Math.Max(0, 1 - (Math.Abs((float)1 - Verhouding())));
            Percentage *= Math.Max(0, 1 - Math.Abs(4 - amountOfHoles));
            return Percentage;
        }

        private float BekijkPercentageAudi()
        {
            float Percentage = 1;
            Percentage *= Math.Max(0, 1 - (Math.Abs((float)0.30 - Oppervlakte()))*(float)0.8);    
            Percentage *= Math.Max(0, 1 - (Math.Abs((float)3 - Verhouding()))*(float)0.3);
            Percentage *= Math.Max(0, 1 - Math.Abs(7 - amountOfHoles));
            return Percentage;
        }

        private float BekijkPercentageMazda()
        {
            float Percentage = 1;
            Percentage *= Math.Max(0, 1 - (Math.Abs((float)0.34 - Oppervlakte())));   
            Percentage *= Math.Max(0, 1 - (Math.Abs((float)1 - Verhouding())));
            Percentage *= Math.Max(0, 1 - (Math.Abs(2 - amountOfHoles) * (float)0.10));
            return Percentage;
        }

        // De verhouding van het object.
        private float Verhouding()
        {
            float verhouding;
            verhouding = ((float)(endx-beginx)/((float)(endy-beginy)));

            return verhouding;
        }

        // De oppervlakte van het object.
        private float Oppervlakte()
        {
            int oppervlakte = 0;
            for(int x = 0; x<endx - beginx; x++)
            {
                for(int y = 0; y < endy-beginy; y++)
                {
                    if (Object[x, y] == Color.FromArgb(255, 255, 255))
                        oppervlakte += 1;
                }
            }
            return ((float)oppervlakte)/((float) ((endx-beginx)*(endy-beginy)));
        }

        public Point FindWhitePixel()
        {
            Point point = new Point(0,0);
            for(int x = 0; x < endx-beginx; x++)
            {
                if(Object[x,0] == Color.FromArgb(255,255,255))
                {
                    point = new Point(x+beginx,0+beginy);
                    break;
                }
            }
            return point;
        }

    }

}

