using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace SimpleImageSteg
{
    class Program
    {
        static void Main( string[] args )
        {
            EncodeMessage( "testimage.png", "text.txt" ).Save( "output.png" );
            File.WriteAllText( "output.txt", DecodeMessage( "output.png" ) );
        }

        public static Bitmap EncodeMessage( string imageFileName, string messageFileName )
        {
            string toHide = File.ReadAllText( messageFileName, Encoding.ASCII );
            byte[] textBytes = textToBytes( toHide );
            byte[] stupidBytes = bytesToStupidBytes( textBytes );

            Bitmap bmp = new Bitmap( imageFileName );
            if ( bmp.PixelFormat != PixelFormat.Format32bppArgb )
                throw new NotImplementedException( "I have not yet gotten around to implement any other pixel formats than 32bppArgb." );

            var bmpData = bmp.LockBits( new Rectangle( 0, 0, bmp.Width, bmp.Height ), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb );
            {
                int bytesCounter = 0;
                var ptr = bmpData.Scan0;
                for ( int i = 0; i < bmpData.Width * bmpData.Height; ++i )
                {
                    int val = Marshal.ReadInt32( ptr );
                    int oldVal = val;
                    byte b1 = (byte)( ( val & 0x000000FF ) >> 0 );  // b
                    byte b2 = (byte)( ( val & 0x0000FF00 ) >> 8 );  // g
                    byte b3 = (byte)( ( val & 0x00FF0000 ) >> 16 ); // r
                    byte b4 = (byte)( ( val & 0xFF000000 ) >> 24 ); // a

                    if ( bytesCounter < stupidBytes.Length )
                        b1 = alterBits( b1, stupidBytes[ bytesCounter++ ] );
                    if ( bytesCounter < stupidBytes.Length )
                        b2 = alterBits( b2, stupidBytes[ bytesCounter++ ] );
                    if ( bytesCounter < stupidBytes.Length )
                        b3 = alterBits( b3, stupidBytes[ bytesCounter++ ] );
                    if ( bytesCounter < stupidBytes.Length )
                        b4 = alterBits( b4, stupidBytes[ bytesCounter++ ] );

                    val = ( ( b4 << 24 ) | ( b3 << 16 ) | ( b2 << 8 ) | ( b1 << 0 ) );

                    Marshal.WriteInt32( ptr, val );
                    ptr = IntPtr.Add( ptr, 1 * sizeof( int ) );
                }
            }
            bmp.UnlockBits( bmpData );

            Console.WriteLine( "FIN" );
            return bmp;
        }

        public static string DecodeMessage( string imageFileName )
        {
            StringBuilder builder = new StringBuilder();
            Bitmap bmp = new Bitmap( imageFileName );

            BitmapData bmpData = bmp.LockBits( new Rectangle( 0, 0, bmp.Width, bmp.Height ), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb );
            {
                IntPtr ptr = bmpData.Scan0;
                for ( int i = 0; i < bmpData.Width * bmpData.Height; ++i )
                {
                    byte character = 0;

                    int val = Marshal.ReadInt32( ptr );
                    byte b1 = (byte)( ( val & 0x000000FF ) >> 0 );  // b
                    byte b2 = (byte)( ( val & 0x0000FF00 ) >> 8 );  // g
                    byte b3 = (byte)( ( val & 0x00FF0000 ) >> 16 ); // r
                    byte b4 = (byte)( ( val & 0xFF000000 ) >> 24 ); // a

                    character = (byte)( ( ( b1 & 0b00000011 ) << 0 ) | ( ( b2 & 0b00000011 ) << 2 ) | ( ( b3 & 0b00000011 ) << 4 ) | ( ( b4 & 0b00000011 ) << 6 ) );
                    builder.Append( Encoding.ASCII.GetString( new[]{character} ) );

                    ptr = IntPtr.Add( ptr, 1 * sizeof( int ) );
                }
            }
            bmp.UnlockBits( bmpData );

            return builder.ToString();
        }

        private static byte[] textToBytes( string text ) => Encoding.ASCII.GetBytes( text );

        private static byte[] bytesToStupidBytes( byte[] bytes )
        {
            byte[] result = new byte[ bytes.Length * 4 ];

            int i = 0;
            foreach( byte b in bytes )
            {
                result[ i++ ] = (byte)( ( b & 0b00000011 ) >> 0 );
                result[ i++ ] = (byte)( ( b & 0b00001100 ) >> 2 );
                result[ i++ ] = (byte)( ( b & 0b00110000 ) >> 4 );
                result[ i++ ] = (byte)( ( b & 0b11000000 ) >> 6 );
            }

            return result;
        }

        // toAdd always has to be 000000XX with only XX holding information
        private static byte alterBits( byte toAlter, byte toAdd )
        {
            if ( ( toAdd & 0b11111111 ) == 0b11111111 )
                throw new Exception( "Wrong format for toAdd." );

            byte result = toAlter;
            if ( toAdd.IsBitSetTo1( 0 ) )
                result = result.SetBitTo1( 0 );
            else
                result = result.SetBitTo0( 0 );

            if ( toAdd.IsBitSetTo1( 1 ) )
                result = result.SetBitTo1( 1 );
            else
                result = result.SetBitTo0( 1 );

            return result;
        }
    }

    public static class ext
    {
        public static string ToBinaryString( this byte b ) => Convert.ToString( b, 2 ).PadLeft( 8, '0' );

        // following 4 functions shamelessly stolen from https://www.dotnetperls.com/set-bit-zero
        public static byte SetBitTo1(this byte value, byte position)
        {
            // Set a bit at position to 1.
            return ( value |= (byte)(1 << position) );
        }

        public static byte SetBitTo0(this byte value, byte position)
        {
            // Set a bit at position to 0.
            return (byte)( value & ~(1 << position) );
        }

        public static bool IsBitSetTo1(this byte value, byte position)
        {
            // Return whether bit at position is set to 1.
            return (value & (1 << position)) != 0;
        }

        public static bool IsBitSetTo0(this byte value, byte position)
        {
            // If not 1, bit is 0.
            return !IsBitSetTo1(value, position);
        }
    }
}
