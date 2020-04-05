﻿/*******************************************************************************
*                                                                              *
* Author    :  Angus Johnson                                                   *
* Version   :  6.4.2                                                           *
* Date      :  27 February 2017                                                *
* Website   :  http://www.angusj.com                                           *
* Copyright :  Angus Johnson 2010-2017                                         *
*                                                                              *
* License:                                                                     *
* Use, modification & distribution is subject to Boost Software License Ver 1. *
* http://www.boost.org/LICENSE_1_0.txt                                         *
*                                                                              *
* Attributions:                                                                *
* The code in this library is an extension of Bala Vatti's clipping algorithm: *
* "A generic solution to polygon clipping"                                     *
* Communications of the ACM, Vol 35, Issue 7 (July 1992) pp 56-63.             *
* http://portal.acm.org/citation.cfm?id=129906                                 *
*                                                                              *
* Computer graphics and geometric modeling: implementation and algorithms      *
* By Max K. Agoston                                                            *
* Springer; 1 edition (January 4, 2005)                                        *
* http://books.google.com/books?q=vatti+clipping+agoston                       *
*                                                                              *
* See also:                                                                    *
* "Polygon Offsetting by Computing Winding Numbers"                            *
* Paper no. DETC2005-85513 pp. 565-575                                         *
* ASME 2005 International Design Engineering Technical Conferences             *
* and Computers and Information in Engineering Conference (IDETC/CIE2005)      *
* September 24-28, 2005 , Long Beach, California, USA                          *
* http://www.me.berkeley.edu/~mcmains/pubs/DAC05OffsetPolygon.pdf              *
*                                                                              *
*******************************************************************************/

/*******************************************************************************
*                                                                              *
* This is a translation of the Delphi Clipper library and the naming style     *
* used has retained a Delphi flavour.                                          *
*                                                                              *
*******************************************************************************/

/*******************************************************************************
* Boost Software License - Version 1.0 - August 17th, 2003                     *
*                                                                              *
* Permission is hereby granted, free of charge, to any person or organization  *
* obtaining a copy of the software and accompanying documentation covered by   *
* this license (the "Software") to use, reproduce, display, distribute,        *
* execute, and transmit the Software, and to prepare derivative works of the   *
* Software, and to permit third-parties to whom the Software is furnished to   *
* do so, all subject to the following:                                         *
*                                                                              *
* The copyright notices in the Software and this entire statement, including   *
* the above license grant, this restriction and the following disclaimer,      *
* must be included in all copies of the Software, in whole or in part, and     *
* all derivative works of the Software, unless such copies or derivative       *
* works are solely in the form of machine-executable object code generated by  *
* a source language processor.                                                 *
*                                                                              *
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR   *
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,     *
* FITNESS FOR A PARTICULAR PURPOSE, TITLE AND NON-INFRINGEMENT. IN NO EVENT    *
* SHALL THE COPYRIGHT HOLDERS OR ANYONE DISTRIBUTING THE SOFTWARE BE LIABLE    *
* FOR ANY DAMAGES OR OTHER LIABILITY, WHETHER IN CONTRACT, TORT OR OTHERWISE,  *
* ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER  *
* DEALINGS IN THE SOFTWARE.                                                    *
*******************************************************************************/

/*******************************************************************************
*                                                                              *
* Code modified for PdfPig                                                     *
*                                                                              *
*******************************************************************************/
namespace UglyToad.PdfPig.Geometry.ClipperLibrary
{
    using System;

    internal class ClipperException : Exception
    {
        public ClipperException(string description) : base(description)
        {
        }
    }
}