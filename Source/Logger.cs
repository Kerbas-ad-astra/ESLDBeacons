﻿/* Copyright 2015 charfa.
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * 
 * */

using System;

namespace ESLDCore
{
    class Logger
    {
        public string Prefix
        {
            get;
            set;
        }

        public Logger(string prefix = "")
        {
            this.Prefix = prefix;
        }

        public void Debug(object message, UnityEngine.Object context = null)
        {
#if DEBUG
            UnityEngine.Debug.Log(Prefix + message, context);
#endif
        }

        public void Info(object message, UnityEngine.Object context = null)
        {
            UnityEngine.Debug.Log(Prefix + message, context);
        }

        public void Warning(object message, UnityEngine.Object context = null)
        {
            UnityEngine.Debug.LogWarning(Prefix + message, context);
        }

        public void Error(object message, UnityEngine.Object context = null)
        {
            UnityEngine.Debug.LogError(Prefix + message, context);
        }
    }
}